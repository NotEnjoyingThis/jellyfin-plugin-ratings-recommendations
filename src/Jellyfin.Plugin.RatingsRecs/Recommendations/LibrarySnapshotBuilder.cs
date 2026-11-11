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
    /// <param name="includeSeasons">Whether to add seasons with features aggregated from their episodes (per-season modes).</param>
    /// <param name="settings">The engine settings (season feature limits).</param>
    /// <returns>The snapshot.</returns>
    public LibrarySnapshot Build(User user, bool includeSeasons, EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(settings);
        var items = _library.GetItems(user, Rateable);
        var people = _library.GetPeople(items);
        var collections = CollectionsByItem(user);

        var playedMovies = _library.GetItems(user, [BaseItemKind.Movie], isPlayed: true);

        // Series and seasons are "watched" when all their (real, not missing) episodes are: computed here because the
        // IsPlayed query filter ignores folders on Jellyfin 10.11 (see docs/NOTES.md).
        var playedEpisodes = _library.GetItems(user, [BaseItemKind.Episode], isPlayed: true).OfType<Episode>().Where(e => !e.IsVirtualItem).ToList();
        var allEpisodes = _library.GetItems(user, [BaseItemKind.Episode]).OfType<Episode>().Where(e => !e.IsVirtualItem).ToList();
        var episodesPerSeries = allEpisodes
            .GroupBy(e => e.SeriesId)
            .ToDictionary(g => g.Key, g => g.Count());
        var playedSeries = playedEpisodes
            .GroupBy(e => e.SeriesId)
            .Where(g => g.Count() >= episodesPerSeries.GetValueOrDefault(g.Key))
            .Select(g => g.Key)
            .ToHashSet();
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
                IsAnthology = kind == "Series" && IsAnthology(item),
            });
        }

        if (includeSeasons)
        {
            AddSeasons(user, settings, mediaItems, allEpisodes, playedEpisodes, facts);
        }

        return new LibrarySnapshot(mediaItems, kinds, facts);
    }

    /// <summary>
    /// Counts the seasons of the whole library that have no per-episode credits (settings page warning).
    /// </summary>
    /// <returns>The report.</returns>
    public SeasonCreditsReport CreditsReport()
    {
        var episodes = _library.GetItems(null, [BaseItemKind.Episode]).OfType<Episode>().ToList();
        var people = _library.GetPeople(episodes);
        var seasons = _library.GetItems(null, [BaseItemKind.Season]).Select(s => s.Id).ToList();
        var bySeason = episodes.GroupBy(e => e.SeasonId).ToDictionary(g => g.Key, g => g.Select(e => Credits(e, people)).ToList());
        var without = seasons.Count(id => !SeasonFeatureBuilder.HasEpisodeCredits(bySeason.GetValueOrDefault(id) ?? []));
        return new SeasonCreditsReport(seasons.Count, without);
    }

    private static bool IsAnthology(BaseItem series)
        => (series.Tags ?? []).Any(t => string.Equals(t, "anthology", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "antologia", StringComparison.OrdinalIgnoreCase));

    private static EpisodeCredits Credits(Episode episode, IReadOnlyDictionary<Guid, IReadOnlyList<PersonInfo>> people)
    {
        var credits = people.GetValueOrDefault(episode.Id) ?? [];
        return new EpisodeCredits
        {
            Directors = Names(credits, PersonKind.Director),
            Writers = Names(credits, PersonKind.Writer),
            GuestStars = Names(credits, PersonKind.GuestStar),
            CommunityRating = episode.CommunityRating,
            Year = episode.PremiereDate?.Year ?? episode.ProductionYear,
        };
    }

    private void AddSeasons(User user, EngineSettings settings, List<MediaItem> items, List<Episode> allEpisodes, List<Episode> playedEpisodes, List<PlaybackFact> facts)
    {
        var seriesById = items.Where(i => i.Kind == "Series").ToDictionary(i => i.Id);
        var people = _library.GetPeople(allEpisodes);
        var episodesBySeason = allEpisodes.GroupBy(e => e.SeasonId).ToDictionary(g => g.Key, g => g.ToList());
        var playedBySeason = playedEpisodes.GroupBy(e => e.SeasonId).ToDictionary(g => g.Key, g => g.ToList());
        var playedSeasons = playedBySeason
            .Where(p => p.Value.Count >= (episodesBySeason.GetValueOrDefault(p.Key)?.Count ?? int.MaxValue))
            .Select(p => p.Key)
            .ToHashSet();

        foreach (var season in _library.GetItems(user, [BaseItemKind.Season]).OfType<Season>())
        {
            if (!seriesById.TryGetValue(season.SeriesId, out var series))
            {
                continue;
            }

            var episodes = episodesBySeason.GetValueOrDefault(season.Id) ?? [];
            var credits = episodes.OrderBy(e => e.IndexNumber ?? int.MaxValue).Select(e => Credits(e, people)).ToList();
            var played = playedSeasons.Contains(season.Id);
            items.Add(SeasonFeatureBuilder.Build(series, new SeasonInfo(season.Id, season.IndexNumber, season.ProductionYear, played), credits, settings));

            // Implicit season signals (section 4.1): watched to the end, or abandoned below 20% of the episodes.
            if (playedBySeason.TryGetValue(season.Id, out var watched) && episodes.Count > 0)
            {
                var last = watched.Select(e => _userData.Get(user, e)?.LastPlayedDate).Where(d => d.HasValue).DefaultIfEmpty().Max();
                facts.Add(new PlaybackFact(season.Id, played, (double)watched.Count / episodes.Count, last));
            }
        }
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
