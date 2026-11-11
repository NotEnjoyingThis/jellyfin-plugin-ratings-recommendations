using System;
using System.Collections.Generic;
using System.Linq;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// "Rate these": what the user has watched but not rated yet, from Jellyfin's own played state (which also covers
/// what was watched before the plugin was installed). See docs/NOTES.md, deviation 43.
/// <list type="bullet">
/// <item>Movie: played and not rated.</item>
/// <item>Series ("Whole series" and "Both" modes): at least one episode watched and the series not rated.</item>
/// <item>Season ("Per season" and "Both" modes): every episode watched and the season not rated.</item>
/// </list>
/// Most recently watched first; items without a date last, by title.
/// </summary>
public static class RateTheseList
{
    /// <summary>
    /// Builds the list.
    /// </summary>
    /// <param name="facts">Playback facts of the user (from the library snapshot).</param>
    /// <param name="items">The items of the snapshot (kind and name).</param>
    /// <param name="mode">The user's series rating mode.</param>
    /// <param name="rated">Items with a stored rating (explicit or implicit).</param>
    /// <returns>The item ids, in display order.</returns>
    public static IReadOnlyList<Guid> Build(IEnumerable<PlaybackFact> facts, IReadOnlyList<MediaItem> items, SeriesRatingMode mode, IReadOnlySet<Guid> rated)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(rated);
        var byId = new Dictionary<Guid, MediaItem>();
        foreach (var item in items)
        {
            byId.TryAdd(item.Id, item);
        }

        return facts
            .GroupBy(f => f.ItemId)
            .Select(g => (Fact: g.First(), Item: byId.GetValueOrDefault(g.Key)))
            .Where(x => x.Item is not null && !rated.Contains(x.Fact.ItemId) && Qualifies(x.Item.Kind, x.Fact, mode))
            .OrderBy(x => x.Fact.LastPlayedUtc is null)
            .ThenByDescending(x => x.Fact.LastPlayedUtc)
            .ThenBy(x => x.Item!.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Fact.ItemId)
            .Select(x => x.Fact.ItemId)
            .ToList();
    }

    private static bool Qualifies(string kind, PlaybackFact fact, SeriesRatingMode mode) => kind switch
    {
        "Movie" => fact.Played,
        "Series" => mode != SeriesRatingMode.Season && (fact.Played || fact.Progress > 0),
        "Season" => mode != SeriesRatingMode.Series && fact.Played,
        _ => false,
    };
}
