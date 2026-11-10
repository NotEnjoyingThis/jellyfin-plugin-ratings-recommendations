using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// The content-based engine of docs/DESIGN.md section 5. Pure and deterministic: the same request
/// (library, ratings, user key, day) always gives the same result, whatever the input order.
/// </summary>
public static class RecommendationEngine
{
    /// <summary>
    /// Ranks the candidates of a user.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The ranking with explanations.</returns>
    public static RecommendationResult Recommend(RecommendationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = request.Settings;
        var index = new FeatureIndex(request.Library, settings);
        var ratings = Normalize(request.Ratings);
        var explicitCount = ratings.Count(r => r.IsExplicit);
        var coldStart = explicitCount < settings.ColdStartThreshold;
        var profile = TasteProfile.Build(index, ratings, request.NowUtc, settings);

        var hatedFeatures = HatedFeatures(index, ratings);
        var hatedCollections = hatedFeatures.Where(f => f.Category == FeatureCategory.Collection).ToHashSet();
        var rated = ratings.Select(r => r.ItemId).ToHashSet();

        // Rule 1: hard exclusions (rated, watched, other chapters of a hated saga). Inaccessible items are
        // never in the library passed by the caller.
        var candidates = index.Items
            .Where(item => !item.Played && !rated.Contains(item.Id))
            .Where(item => !settings.ExcludeHatedCollections || !index.Features(item.Id).Any(hatedCollections.Contains))
            .ToList();

        var breakdowns = candidates.ToDictionary(c => c.Id, c => TasteScorer.Score(index, profile, c, settings));
        var min = breakdowns.Count == 0 ? 0 : breakdowns.Values.Min(b => b.Total);
        var max = breakdowns.Count == 0 ? 0 : breakdowns.Values.Max(b => b.Total);
        var averageQuality = AverageQuality(index.Items);
        var (wTaste, wQuality, wExploration) = coldStart
            ? (settings.ColdTasteWeight, settings.ColdQualityWeight, settings.ColdExplorationWeight)
            : (settings.TasteWeight, settings.QualityWeight, settings.ExplorationWeight);

        var scored = new List<ScoredItem>(candidates.Count);
        foreach (var item in candidates)
        {
            var breakdown = breakdowns[item.Id];
            var taste = max - min > 1e-12 ? (breakdown.Total - min) / (max - min) : 0.5;

            // Rule 2: proximity penalty, applied to the normalised taste so it always lowers it.
            var penalized = index.Features(item.Id).Any(f => (f.Category is FeatureCategory.Director or FeatureCategory.Collection) && hatedFeatures.Contains(f));
            if (penalized)
            {
                taste *= settings.ProximityPenalty;
            }

            var quality = item.CommunityRating is double r ? Math.Clamp(r / 10.0, 0, 1) : averageQuality;
            var exploration = StableRandom.Unit(request.UserKey, request.NowUtc, item.Id);
            scored.Add(new ScoredItem
            {
                ItemId = item.Id,
                RawTaste = breakdown.Total,
                Taste = taste,
                Quality = quality,
                Exploration = exploration,
                Penalized = penalized,
                Score = (wTaste * taste) + (wQuality * quality) + (wExploration * exploration),
                Explanation = Explain(index, breakdown, ratings, settings),
            });
        }

        // Rule 3: diversity (MMR on the top items) and the saga cap.
        var byId = scored.ToDictionary(s => s.ItemId);
        var order = Diversifier.Rerank(
            scored.Select(s => new DiversityCandidate(
                s.ItemId,
                s.Score,
                index.Vector(s.ItemId),
                index.Features(s.ItemId).Where(f => f.Category == FeatureCategory.Collection).Select(f => f.Key).ToList())).ToList(),
            settings.DiversityLambda,
            settings.DiversityPoolSize,
            settings.MaxPerCollection,
            settings.CollectionCapWindow);

        return new RecommendationResult
        {
            Items = order.Select(id => byId[id]).ToList(),
            IsColdStart = coldStart,
            ExplicitRatingCount = explicitCount,
        };
    }

    /// <summary>
    /// Keeps one rating per item (explicit wins over implicit, then the most recent) in a stable order.
    /// </summary>
    /// <param name="ratings">The ratings.</param>
    /// <returns>The normalised ratings.</returns>
    public static IReadOnlyList<UserRating> Normalize(IEnumerable<UserRating> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        return ratings
            .GroupBy(r => r.ItemId)
            .Select(g => g.OrderByDescending(r => r.IsExplicit).ThenByDescending(r => r.TimestampUtc).ThenByDescending(r => r.Value).First())
            .OrderBy(r => r.ItemId)
            .ToList();
    }

    private static HashSet<Feature> HatedFeatures(FeatureIndex index, IEnumerable<UserRating> ratings)
    {
        var features = new HashSet<Feature>();
        foreach (var rating in ratings.Where(r => r.IsHate))
        {
            foreach (var feature in index.Features(rating.ItemId))
            {
                if (feature.Category is FeatureCategory.Director or FeatureCategory.Collection)
                {
                    features.Add(feature);
                }
            }
        }

        return features;
    }

    private static double AverageQuality(IEnumerable<MediaItem> items)
    {
        var ratings = items.Where(i => i.CommunityRating.HasValue).Select(i => Math.Clamp(i.CommunityRating!.Value / 10.0, 0, 1)).ToList();
        return ratings.Count == 0 ? 0.5 : ratings.Average();
    }

    private static Explanation Explain(FeatureIndex index, TasteBreakdown breakdown, IReadOnlyList<UserRating> ratings, EngineSettings settings)
    {
        var top = breakdown.Top(settings.ExplanationFeatures);
        if (top.Count == 0)
        {
            return new Explanation();
        }

        // "Because you loved X": the best-rated liked item that shares the top feature.
        var lead = top[0].Feature;
        var because = ratings
            .Where(r => r.Value > 0 && (r.IsLoved || !r.IsExplicit))
            .Where(r => index.Features(r.ItemId).Contains(lead))
            .OrderByDescending(r => r.IsExplicit)
            .ThenByDescending(r => r.Value)
            .ThenByDescending(r => r.TimestampUtc)
            .ThenBy(r => r.ItemId)
            .FirstOrDefault();

        return new Explanation
        {
            TopFeatures = top.Select(t => new FeatureContribution(t.Feature.Category, t.Feature.Display, t.Contribution)).ToList(),
            BecauseOfItemId = because?.ItemId,
            BecauseOfName = because is null ? null : index.Item(because.ItemId)?.Name,
            BecauseOfValue = because?.Value,
        };
    }
}
