using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.RatingsRecs.Compat;
using MediaBrowser.Controller.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Application service behind the ratings API: validation, access checks, store and native mirror.
/// </summary>
public sealed class RatingsService
{
    private const int MaxReportedUnmatched = 20;

    private static readonly BaseItemKind[] MatchableKinds = [BaseItemKind.Movie, BaseItemKind.Series];

    private readonly IRatingStore _store;
    private readonly NativeMirror _mirror;
    private readonly ILibraryGateway _library;
    private readonly TimeProvider _clock;
    private readonly ILogger<RatingsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingsService"/> class.
    /// </summary>
    /// <param name="store">The rating store.</param>
    /// <param name="mirror">The native mirror.</param>
    /// <param name="library">The library gateway.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public RatingsService(IRatingStore store, NativeMirror mirror, ILibraryGateway library, TimeProvider clock, ILogger<RatingsService> logger)
    {
        _store = store;
        _mirror = mirror;
        _library = library;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets a rating.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>The rating or <c>null</c>.</returns>
    public RatingEntry? Get(Guid userId, Guid itemId) => _store.Get(userId, itemId);

    /// <summary>
    /// Gets several ratings.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemIds">The items.</param>
    /// <returns>Ratings of the rated items.</returns>
    public IReadOnlyDictionary<Guid, RatingEntry> GetMany(Guid userId, IEnumerable<Guid> itemIds) => _store.GetMany(userId, itemIds);

    /// <summary>
    /// Sets or changes the rating of an item the user can access.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="value">The level.</param>
    /// <param name="entry">The stored rating on success.</param>
    /// <returns>The outcome.</returns>
    public RatingOutcome Set(User user, Guid itemId, int value, out RatingEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(user);
        entry = null;
        if (!RatingLevels.IsValid(value))
        {
            return RatingOutcome.InvalidValue;
        }

        var item = FindVisible(user, itemId);
        if (item is null)
        {
            return RatingOutcome.NotFound;
        }

        var itemType = RateableItems.GetItemType(item);
        if (itemType is null)
        {
            return RatingOutcome.NotRateable;
        }

        entry = _store.SetExplicit(user.Id, item.Id, itemType, value);
        _mirror.Apply(user, item, value);
        return RatingOutcome.Ok;
    }

    /// <summary>
    /// Removes the rating of an item.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>The removed rating, or <c>null</c>.</returns>
    public RatingEntry? Remove(User user, Guid itemId)
    {
        ArgumentNullException.ThrowIfNull(user);
        var removed = _store.Remove(user.Id, itemId);
        if (removed is not null)
        {
            var item = FindVisible(user, itemId);
            if (item is not null)
            {
                _mirror.Apply(user, item, null);
            }
        }

        return removed;
    }

    /// <summary>
    /// Converts the user's Favourite movies and series without an explicit rating into +3 ratings.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>Counts.</returns>
    public FavoritesImportResult ImportFavorites(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var imported = 0;
        var alreadyRated = 0;
        foreach (var item in _library.GetItems(user, MatchableKinds, isFavorite: true))
        {
            var itemType = RateableItems.GetItemType(item);
            if (itemType is null)
            {
                continue;
            }

            if (_store.Get(user.Id, item.Id) is { IsExplicit: true })
            {
                alreadyRated++;
                continue;
            }

            _store.SetExplicit(user.Id, item.Id, itemType, RatingLevels.Amazing);
            _mirror.Apply(user, item, RatingLevels.Amazing);
            imported++;
        }

        _logger.LogInformation("Imported {Count} Favourites as +3 for user {UserId} ({Already} already rated)", imported, user.Id, alreadyRated);
        return new FavoritesImportResult(imported, alreadyRated);
    }

    /// <summary>
    /// Exports a user's ratings as CSV, with descriptive columns to re-match items elsewhere.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>CSV text.</returns>
    public string ExportCsv(Guid userId)
    {
        var rows = _store.GetAll(userId)
            .OrderBy(e => e.CreatedUtc)
            .Select(e =>
            {
                var item = _library.GetItem(e.ItemId);
                return new RatingCsvRow
                {
                    Entry = e,
                    Name = item?.Name,
                    Year = item?.ProductionYear,
                    ImdbId = ProviderId(item, "Imdb"),
                    TmdbId = ProviderId(item, "Tmdb"),
                    TvdbId = ProviderId(item, "Tvdb"),
                };
            });
        return RatingsCsv.Write(rows);
    }

    /// <summary>
    /// Imports ratings from CSV: items are matched by id, then provider ids, then name + year;
    /// the most recent rating wins; explicit ratings are mirrored onto native user data.
    /// </summary>
    /// <param name="user">The user receiving the ratings.</param>
    /// <param name="csv">The CSV text.</param>
    /// <returns>Counts and problems.</returns>
    public CsvImportResult ImportCsv(User user, string csv)
    {
        ArgumentNullException.ThrowIfNull(user);
        var parsed = RatingsCsv.Parse(csv);
        var matcher = new Lazy<ItemMatcher>(() => new ItemMatcher(_library.GetItems(null, MatchableKinds)));
        var now = _clock.GetUtcNow().UtcDateTime;
        var matched = new List<RatingEntry>();
        var unmatched = new List<string>();

        foreach (var row in parsed.Rows)
        {
            var item = row.Entry.ItemId != Guid.Empty ? _library.GetItem(row.Entry.ItemId) : null;
            if (item is not null && !string.Equals(TypeName(item), row.Entry.ItemType, StringComparison.Ordinal))
            {
                item = null;
            }

            if (item is null && row.Entry.ItemType != "Season")
            {
                item = matcher.Value.Find(row);
            }

            if (item is null)
            {
                unmatched.Add(row.Name ?? row.Entry.ItemId.ToString());
                continue;
            }

            var entry = row.Entry with { ItemId = item.Id };
            if (entry.UpdatedUtc == DateTime.MinValue)
            {
                entry = entry with { CreatedUtc = now, UpdatedUtc = now };
            }

            matched.Add(entry);
        }

        var merge = _store.Merge(user.Id, matched);

        foreach (var entry in matched.Where(e => e.IsExplicit))
        {
            if (_store.Get(user.Id, entry.ItemId) is { IsExplicit: true } current && current.Value == entry.Value)
            {
                var item = FindVisible(user, entry.ItemId);
                if (item is not null && RateableItems.GetItemType(item) is not null)
                {
                    _mirror.Apply(user, item, current.Value);
                }
            }
        }

        _logger.LogInformation(
            "CSV import for user {UserId}: {Added} added, {Updated} updated, {Skipped} skipped, {Unmatched} unmatched, {Errors} invalid lines",
            user.Id,
            merge.Added,
            merge.Updated,
            merge.Skipped,
            unmatched.Count,
            parsed.Errors.Count);

        return new CsvImportResult(
            merge.Added,
            merge.Updated,
            merge.Skipped,
            unmatched.Count,
            unmatched.Take(MaxReportedUnmatched).ToList(),
            parsed.Errors.Take(MaxReportedUnmatched).ToList());
    }

    private static string? ProviderId(BaseItem? item, string key)
        => item?.ProviderIds is { } ids && ids.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string TypeName(BaseItem item) => item.GetBaseItemKind() switch
    {
        BaseItemKind.Movie => "Movie",
        BaseItemKind.Series => "Series",
        BaseItemKind.Season => "Season",
        _ => string.Empty,
    };

    private BaseItem? FindVisible(User user, Guid itemId)
    {
        var item = _library.GetItem(itemId);
        return item is not null && item.IsVisibleStandalone(user) ? item : null;
    }

    private sealed class ItemMatcher
    {
        private readonly Dictionary<string, BaseItem> _byKey = new(StringComparer.OrdinalIgnoreCase);

        public ItemMatcher(IReadOnlyList<BaseItem> items)
        {
            foreach (var item in items)
            {
                var type = TypeName(item);
                foreach (var provider in new[] { "Imdb", "Tmdb", "Tvdb" })
                {
                    var id = ProviderId(item, provider);
                    if (id is not null)
                    {
                        _byKey.TryAdd(type + "|" + provider + "|" + id, item);
                    }
                }

                _byKey.TryAdd(type + "|name|" + item.Name + "|" + item.ProductionYear, item);
            }
        }

        public BaseItem? Find(RatingCsvRow row)
        {
            var type = row.Entry.ItemType;
            foreach (var (provider, id) in new[] { ("Imdb", row.ImdbId), ("Tmdb", row.TmdbId), ("Tvdb", row.TvdbId) })
            {
                if (id is not null && _byKey.TryGetValue(type + "|" + provider + "|" + id, out var byProvider))
                {
                    return byProvider;
                }
            }

            return row.Name is not null && _byKey.TryGetValue(type + "|name|" + row.Name + "|" + row.Year, out var byName) ? byName : null;
        }
    }
}
