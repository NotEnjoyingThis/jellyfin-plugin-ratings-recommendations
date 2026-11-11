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
    /// <returns>The ranked items, best first (empty if the source is unknown).</returns>
    public static IReadOnlyList<ScoredItem> Rank(EngineContext context, Guid sourceId, int limit, int poolSize = DefaultPoolSize)
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
}
