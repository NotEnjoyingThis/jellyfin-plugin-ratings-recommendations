using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.RatingsRecs.Compat;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Reads the library of a user (only what the user can access: library restrictions and parental control
/// apply through the user-scoped queries) and converts it into engine DTOs. Runs in the background only.
/// </summary>
public sealed class LibrarySnapshotBuilder
{
    private static readonly BaseItemKind[] Rateable = [BaseItemKind.Movie, BaseItemKind.Series];

    private readonly ILibraryGateway _library;
    private readonly IUserDataGateway _userData;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySnapshotBuilder"/> class.
    /// </summary>
    /// <param name="library">The library gateway.</param>
    /// <param name="userData">The user data gateway.</param>
    public LibrarySnapshotBuilder(ILibraryGateway library, IUserDataGateway userData)
    {
        _library = library;
        _userData = userData;
    }

    /// <summary>
    /// Builds the snapshot of a user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The snapshot.</returns>
    public LibrarySnapshot Build(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var items = _library.GetItems(user, Rateable);
        var people = _library.GetPeople(items);
        var collections = CollectionsByItem(user);

        var playedMovies = _library.GetItems(user, [BaseItemKind.Movie], isPlayed: true);
        var playedSeries = _library.GetItems(user, [BaseItemKind.Series], isPlayed: true).Select(s => s.Id).ToHashSet();
        var playedEpisodes = _library.GetItems(user, [BaseItemKind.Episode], isPlayed: true).OfType<Episode>().ToList();
        var episodesPerSeries = _library.GetItems(user, [BaseItemKind.Episode]).OfType<Episode>()
            .GroupBy(e => e.SeriesId)
            .ToDictionary(g => g.Key, g => g.Count());
        var startedSeries = playedEpisodes.Select(e => e.SeriesId).ToHashSet();

        var facts = new List<PlaybackFact>();
        foreach (var movie in playedMovies)
        {
            facts.Add(new PlaybackFact(movie.Id, true, 1, _userData.Get(user, movie)?.LastPlayedDate));
        }

        foreach (var movie in _library.GetItems(user, [BaseItemKind.Movie], isResumable: true))
        {
            var data = _userData.Get(user, movie);
            if (data is null || movie.RunTimeTicks is not > 0)
            {
                continue;
            }

            facts.Add(new PlaybackFact(movie.Id, false, (double)data.PlaybackPositionTicks / movie.RunTimeTicks.Value, data.LastPlayedDate));
        }

        foreach (var series in playedEpisodes.GroupBy(e => e.SeriesId))
        {
            var total = episodesPerSeries.GetValueOrDefault(series.Key);
            var last = series.Select(e => _userData.Get(user, e)?.LastPlayedDate).Where(d => d.HasValue).DefaultIfEmpty().Max();
            var played = playedSeries.Contains(series.Key);
            facts.Add(new PlaybackFact(series.Key, played, total > 0 ? (double)series.Count() / total : 1, last));
        }

        var kinds = new Dictionary<Guid, string>();
        var mediaItems = new List<MediaItem>(items.Count);
        var playedMovieIds = playedMovies.Select(m => m.Id).ToHashSet();
        foreach (var item in items)
        {
            var kind = item.GetBaseItemKind() == BaseItemKind.Series ? "Series" : "Movie";
            kinds[item.Id] = kind;
            var credits = people.GetValueOrDefault(item.Id) ?? [];
            mediaItems.Add(new MediaItem
            {
                Id = item.Id,
                Kind = kind,
                Name = item.Name ?? string.Empty,
                Genres = item.Genres ?? [],
                Tags = item.Tags ?? [],
                Studios = item.Studios ?? [],
                Directors = Names(credits, PersonKind.Director),
                Actors = Names(credits, PersonKind.Actor),
                Writers = Names(credits, PersonKind.Writer),
                Collections = collections.GetValueOrDefault(item.Id) ?? [],
                Year = item.ProductionYear,
                CommunityRating = item.CommunityRating,

                // "Not watched": movies not played; series with no episode watched (docs/DESIGN.md section 5.3).
                Played = kind == "Series" ? startedSeries.Contains(item.Id) || playedSeries.Contains(item.Id) : playedMovieIds.Contains(item.Id),
            });
        }

        return new LibrarySnapshot(mediaItems, kinds, facts);
    }

    private static List<string> Names(IReadOnlyList<PersonInfo> people, PersonKind kind)
        => people
            .Where(p => p.Type == kind && !string.IsNullOrWhiteSpace(p.Name))
            .OrderBy(p => p.SortOrder ?? int.MaxValue)
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private Dictionary<Guid, List<string>> CollectionsByItem(User user)
    {
        var result = new Dictionary<Guid, List<string>>();
        foreach (var boxSet in _library.GetItems(user, [BaseItemKind.BoxSet]).OfType<Folder>())
        {
            foreach (var child in boxSet.GetLinkedChildren())
            {
                if (!result.TryGetValue(child.Id, out var names))
                {
                    names = [];
                    result[child.Id] = names;
                }

                if (!string.IsNullOrWhiteSpace(boxSet.Name))
                {
                    names.Add(boxSet.Name);
                }
            }
        }

        return result;
    }
}
