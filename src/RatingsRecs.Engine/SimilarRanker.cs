using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// "Similar, picked for you" (docs/DESIGN.md section 6.6, point 3): the items sharing the most features with the
/// open item (at most 200 candidates), ranked with the user's taste profile; items rated negatively never appear.
/// </summary>
public static class SimilarRanker
{
    /// <summary>
    /// The number of most-overlapping candidates that are scored.
    /// </summary>
    public const int DefaultPoolSize = 200;

    /// <summary>
    /// Ranks the items similar to a source item.
    /// </summary>
    /// <param name="context">The user's engine context.</param>
    /// <param name="sourceId">The open item.</param>
    /// <param name="limit">How many items to return.</param>
    /// <param name="poolSize">How many most-overlapping candidates to score.</param>
    /// <param name="kind">Only items of this kind ("Movie", "Series"), or <c>null</c> for any kind.</param>
    /// <returns>The ranked items, best first (empty if the source is unknown).</returns>
    public static IReadOnlyList<ScoredItem> Rank(EngineContext context, Guid sourceId, int limit, int poolSize = DefaultPoolSize, string? kind = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var index = context.Index;
        var settings = context.Request.Settings;
        var sourceFeatures = index.Features(sourceId).ToHashSet();
        if (sourceFeatures.Count == 0 || limit <= 0)
        {
            return [];
        }

        var negative = context.Ratings.Where(r => r.Value < 0).Select(r => r.ItemId).ToHashSet();
        var pool = index.Items
            .Where(i => i.Id != sourceId && !negative.Contains(i.Id))
            .Where(i => kind is null || string.Equals(i.Kind, kind, StringComparison.Ordinal))
            .Select(i => (Item: i, Overlap: index.Features(i.Id).Count(sourceFeatures.Contains)))
            .Where(c => c.Overlap > 0)
            .OrderByDescending(c => c.Overlap)
            .ThenBy(c => c.Item.Id)
            .Take(Math.Max(1, poolSize))
            .ToList();
        if (pool.Count == 0)
        {
            return [];
        }

        var breakdowns = pool.ToDictionary(c => c.Item.Id, c => TasteScorer.Score(index, context.Profile, c.Item, settings));
        var min = breakdowns.Values.Min(b => b.Total);
        var max = breakdowns.Values.Max(b => b.Total);
        var (wTaste, wQuality, wExploration) = context.IsColdStart
            ? (settings.ColdTasteWeight, settings.ColdQualityWeight, settings.ColdExplorationWeight)
            : (settings.TasteWeight, settings.QualityWeight, settings.ExplorationWeight);

        return pool
            .Select(c =>
            {
                var breakdown = breakdowns[c.Item.Id];
                var taste = max - min > 1e-12 ? (breakdown.Total - min) / (max - min) : 0.5;
                var penalized = index.Features(c.Item.Id).Any(f => (f.Category is FeatureCategory.Director or FeatureCategory.Collection) && context.HatedFeatures.Contains(f));
                if (penalized)
                {
                    taste *= settings.ProximityPenalty;
                }

                var quality = c.Item.CommunityRating is double r ? Math.Clamp(r / 10.0, 0, 1) : context.AverageQuality;
                var exploration = StableRandom.Unit(context.Request.UserKey, context.Request.NowUtc, c.Item.Id);
                return (c.Overlap, Scored: new ScoredItem
                {
                    ItemId = c.Item.Id,
                    RawTaste = breakdown.Total,
                    Taste = taste,
                    Quality = quality,
                    Exploration = exploration,
                    Penalized = penalized,
                    Score = (wTaste * taste) + (wQuality * quality) + (wExploration * exploration),
                    Explanation = RecommendationEngine.Explain(index, breakdown, context.Ratings, settings),
                });
            })
            .OrderByDescending(x => x.Scored.Score)
            .ThenByDescending(x => x.Overlap)
            .ThenBy(x => x.Scored.ItemId)
            .Take(limit)
            .Select(x => x.Scored)
            .ToList();
    }

    /// <summary>
    /// Order for the Jellyfin 12 "similar items" provider. Jellyfin appends the results of its own provider whenever ours
    /// returns fewer items than requested, so the list covers every item of the source's kind: first the similar items
    /// (<see cref="Rank"/>), then the others by taste, and only at the end the negatively rated ones (least disliked
    /// first), which callers only reach when the library is too small to fill the request.
    /// Evaluated lazily: the later phases are computed only if enumerated.
    /// </summary>
    /// <param name="context">The user's engine context.</param>
    /// <param name="sourceId">The open item.</param>
    /// <param name="liveNegatives">Negative ratings given since the context was computed (item id to value).</param>
    /// <param name="poolSize">How many most-overlapping candidates to score in the first phase.</param>
    /// <returns>Item ids, best first; never the source, only items of the source's kind.</returns>
    public static IEnumerable<Guid> OrderForProvider(EngineContext context, Guid sourceId, IReadOnlyDictionary<Guid, double> liveNegatives, int poolSize = DefaultPoolSize)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(liveNegatives);
        return OrderForProviderIterator(context, sourceId, liveNegatives, poolSize);
    }

    private static IEnumerable<Guid> OrderForProviderIterator(EngineContext context, Guid sourceId, IReadOnlyDictionary<Guid, double> liveNegatives, int poolSize)
    {
        var index = context.Index;
        var source = index.Items.FirstOrDefault(i => i.Id == sourceId);
        if (source is null)
        {
            yield break;
        }

        var negatives = new Dictionary<Guid, double>();
        foreach (var rating in context.Ratings.Where(r => r.Value < 0))
        {
            negatives[rating.ItemId] = rating.Value;
        }

        foreach (var pair in liveNegatives)
        {
            negatives[pair.Key] = pair.Value;
        }

        var sameKind = index.Items
            .Where(i => i.Id != sourceId && string.Equals(i.Kind, source.Kind, StringComparison.Ordinal))
            .ToList();
        var returned = new HashSet<Guid>();

        // Phase 1: the similar items, ranked with the taste profile.
        foreach (var scored in Rank(context, sourceId, sameKind.Count, poolSize, source.Kind))
        {
            if (!negatives.ContainsKey(scored.ItemId) && returned.Add(scored.ItemId))
            {
                yield return scored.ItemId;
            }
        }

        // Phase 2: every other item of the same kind, by taste (then community rating) - only reached in small libraries.
        var settings = context.Request.Settings;
        var others = sameKind
            .Where(i => !returned.Contains(i.Id) && !negatives.ContainsKey(i.Id))
            .Select(i => (Item: i, Taste: TasteScorer.Score(index, context.Profile, i, settings).Total))
            .OrderByDescending(x => x.Taste)
            .ThenByDescending(x => x.Item.CommunityRating ?? 0)
            .ThenBy(x => x.Item.Id)
            .Select(x => x.Item.Id)
            .ToList();
        foreach (var id in others)
        {
            returned.Add(id);
            yield return id;
        }

        // Phase 3: negatively rated items at the very end, so Jellyfin has nothing left to place among ours.
        foreach (var item in sameKind
            .Where(i => negatives.ContainsKey(i.Id) && !returned.Contains(i.Id))
            .OrderByDescending(i => negatives[i.Id])
            .ThenBy(i => i.Id))
        {
            yield return item.Id;
        }
    }
}
