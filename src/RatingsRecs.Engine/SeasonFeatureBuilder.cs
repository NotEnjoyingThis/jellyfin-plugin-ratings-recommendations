using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// Builds the features of a season (section 5.7): genres, tags, studios and main cast from the series;
/// directors and writers as the union over the episodes; the 5 most frequent guest stars; the decade of the
/// season (or of its first episode); quality = average episode rating. A season without per-episode credits
/// gets the credits of its series, so it behaves as in the "whole series" mode.
/// </summary>
public static class SeasonFeatureBuilder
{
    /// <summary>
    /// Determines whether the episodes carry any credits (directors, writers or guest stars).
    /// </summary>
    /// <param name="episodes">The episodes of a season.</param>
    /// <returns><c>true</c> when at least one episode has credits.</returns>
    public static bool HasEpisodeCredits(IEnumerable<EpisodeCredits> episodes)
        => (episodes ?? []).Any(e => e.Directors.Count > 0 || e.Writers.Count > 0 || e.GuestStars.Count > 0);

    /// <summary>
    /// Builds the engine item of a season.
    /// </summary>
    /// <param name="series">The series item.</param>
    /// <param name="season">The season.</param>
    /// <param name="episodes">The episodes of the season.</param>
    /// <param name="settings">The settings (number of guest stars).</param>
    /// <returns>The season item.</returns>
    public static MediaItem Build(MediaItem series, SeasonInfo season, IReadOnlyList<EpisodeCredits> episodes, EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentNullException.ThrowIfNull(settings);
        var hasCredits = HasEpisodeCredits(episodes);
        var ratings = episodes.Where(e => e.CommunityRating.HasValue).Select(e => e.CommunityRating!.Value).ToList();
        var firstEpisodeYear = episodes.Where(e => e.Year.HasValue).Select(e => e.Year!.Value).DefaultIfEmpty().Min();

        return new MediaItem
        {
            Id = season.Id,
            Kind = "Season",
            Name = series.Name + " S" + (season.IndexNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"),
            ParentId = series.Id,
            ParentName = series.Name,
            IndexNumber = season.IndexNumber,
            Genres = series.Genres,
            Tags = series.Tags,
            Studios = series.Studios,
            Actors = series.Actors.Take(settings.MaxActors).ToList(),
            Collections = series.Collections,
            Directors = hasCredits ? ByFrequency(episodes.SelectMany(e => e.Directors), int.MaxValue) : series.Directors,
            Writers = hasCredits ? ByFrequency(episodes.SelectMany(e => e.Writers), int.MaxValue) : series.Writers,
            GuestStars = hasCredits ? ByFrequency(episodes.SelectMany(e => e.GuestStars), settings.MaxGuestStars) : series.GuestStars,
            Year = season.Year ?? (firstEpisodeYear > 0 ? firstEpisodeYear : series.Year),
            CommunityRating = ratings.Count > 0 ? ratings.Average() : series.CommunityRating,
            Played = season.Played,
            IsAnthology = series.IsAnthology,
        };
    }

    private static List<string> ByFrequency(IEnumerable<string> names, int limit)
        => names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .GroupBy(n => n.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(g => g.Key)
            .ToList();
}
