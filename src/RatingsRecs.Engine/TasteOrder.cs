using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// Reorders a list chosen by someone else (e.g. Jellyfin's "Because you watched…" categories) with the user's taste:
/// negatively rated items are dropped, the others are sorted by taste score (ties keep the original order) and items
/// the engine does not know stay at the end in their original order.
/// </summary>
public static class TasteOrder
{
    /// <summary>
    /// Reorders the items.
    /// </summary>
    /// <param name="context">The user's engine context.</param>
    /// <param name="itemIds">The items, in the original order.</param>
    /// <param name="liveNegatives">Negative ratings given since the context was computed (item id to value).</param>
    /// <returns>The items to show, best first.</returns>
    public static IReadOnlyList<Guid> Order(EngineContext context, IReadOnlyList<Guid> itemIds, IReadOnlyDictionary<Guid, double> liveNegatives)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(itemIds);
        ArgumentNullException.ThrowIfNull(liveNegatives);
        var negative = context.Ratings.Where(r => r.Value < 0).Select(r => r.ItemId).ToHashSet();
        negative.UnionWith(liveNegatives.Keys);
        var items = context.Index.Items.ToDictionary(i => i.Id);
        var settings = context.Request.Settings;

        var kept = itemIds.Distinct().Where(id => !negative.Contains(id)).Select((id, position) => (Id: id, Position: position)).ToList();
        var known = kept
            .Where(x => items.ContainsKey(x.Id))
            .Select(x => (x.Id, x.Position, Taste: TasteScorer.Score(context.Index, context.Profile, items[x.Id], settings).Total))
            .OrderByDescending(x => x.Taste)
            .ThenBy(x => x.Position)
            .Select(x => x.Id);
        var unknown = kept.Where(x => !items.ContainsKey(x.Id)).Select(x => x.Id);
        return known.Concat(unknown).ToList();
    }
}
