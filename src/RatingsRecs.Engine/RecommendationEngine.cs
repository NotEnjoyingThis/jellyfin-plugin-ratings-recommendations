using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// The content-based engine of docs/DESIGN.md section 5 (and 5.7 for seasons). Pure and deterministic: the same
/// request (library, ratings, user key, day, mode) always gives the same result, whatever the input order.
/// </summary>
public static class RecommendationEngine
{
    private const string SeasonKind = "Season";

    /// <summary>
    /// Ranks the candidates of a user.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The ranking with explanations.</returns>
    public static RecommendationResult Recommend(RecommendationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = request.Settings;
        var mode = request.Mode;
        var all = request.Library.GroupBy(i => i.Id).Select(g => g.First()).OrderBy(i => i.Id).ToList();
        var byId = all.ToDictionary(i => i.Id);

        // In the per-season modes a series with seasons is represented by its seasons (section 5.7).
        var seasonsBySeries = mode == SeriesRatingMode.Series
            ? new Dictionary<Guid, List<MediaItem>>()
            : all.Where(i => IsSeasonKind(i) && i.ParentId.HasValue)
                .GroupBy(i => i.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.IndexNumber ?? int.MaxValue).ThenBy(s => s.Id).ToList());
        bool IsSeason(Guid id) => byId.TryGetValue(id, out var item) && IsSeasonKind(item);
        bool IsSplitSeries(Guid id) => seasonsBySeries.ContainsKey(id);

        var indexed = mode == SeriesRatingMode.Series
            ? all.Where(i => !IsSeasonKind(i)).ToList()
            : all.Where(i => !IsSplitSeries(i.Id)).ToList();
        var index = new FeatureIndex(indexed, settings);

        var original = Normalize(request.Ratings);
        var effective = EffectiveRatings(original, mode, IsSeason, IsSplitSeries, seasonsBySeries);
        var counted = mode switch
        {
            SeriesRatingMode.Series => original.Where(r => !IsSeason(r.ItemId)),
            SeriesRatingMode.Season => original.Where(r => !IsSplitSeries(r.ItemId)),
            _ => original,
        };
        var explicitCount = counted.Count(r => r.IsExplicit);
        var coldStart = explicitCount < settings.ColdStartThreshold;
        var profile = TasteProfile.Build(index, effective, request.NowUtc, settings);

        var hatedFeatures = HatedFeatures(index, effective);
        var hatedCollections = hatedFeatures.Where(f => f.Category == FeatureCategory.Collection).ToHashSet();
        var rated = original.Select(r => r.ItemId).ToHashSet();
        var seriesWithRatedSeasons = mode == SeriesRatingMode.Series
            ? new HashSet<Guid>()
            : original.Where(r => IsSeason(r.ItemId)).Select(r => byId[r.ItemId].ParentId).Where(p => p.HasValue).Select(p => p!.Value).ToHashSet();

        bool Excluded(MediaItem item) => item.Played || rated.Contains(item.Id)
            || (settings.ExcludeHatedCollections && index.Features(item.Id).Any(hatedCollections.Contains));

        // Rule 1: hard exclusions. Units are scored items; a split series owns several units (its seasons).
        var units = new List<(MediaItem Unit, Guid Owner)>();
        foreach (var item in index.Items.Where(i => !IsSeasonKind(i)))
        {
            if (!Excluded(item))
            {
                units.Add((item, item.Id));
            }
        }

        foreach (var (seriesId, seasons) in seasonsBySeries.OrderBy(kv => kv.Key))
        {
            if (!byId.TryGetValue(seriesId, out var series) || series.Played || rated.Contains(seriesId) || seriesWithRatedSeasons.Contains(seriesId)
                || (settings.ExcludeHatedCollections && series.Collections.Select(c => Feature.Of(FeatureCategory.Collection, c)).Any(hatedCollections.Contains)))
            {
                continue;
            }

            units.AddRange(seasons.Select(s => (s, seriesId)));
        }

        var breakdowns = units.ToDictionary(u => u.Unit.Id, u => TasteScorer.Score(index, profile, u.Unit, settings));
        var min = breakdowns.Count == 0 ? 0 : breakdowns.Values.Min(b => b.Total);
        var max = breakdowns.Count == 0 ? 0 : breakdowns.Values.Max(b => b.Total);
        var averageQuality = AverageQuality(index.Items);
        var (wTaste, wQuality, wExploration) = coldStart
            ? (settings.ColdTasteWeight, settings.ColdQualityWeight, settings.ColdExplorationWeight)
            : (settings.TasteWeight, settings.QualityWeight, settings.ExplorationWeight);

        ScoredItem ScoreUnit(MediaItem unit)
        {
            var breakdown = breakdowns[unit.Id];
            var taste = max - min > 1e-12 ? (breakdown.Total - min) / (max - min) : 0.5;

            // Rule 2: proximity penalty, applied to the normalised taste so it always lowers it.
            var penalized = index.Features(unit.Id).Any(f => (f.Category is FeatureCategory.Director or FeatureCategory.Collection) && hatedFeatures.Contains(f));
            if (penalized)
            {
                taste *= settings.ProximityPenalty;
            }

            var quality = unit.CommunityRating is double r ? Math.Clamp(r / 10.0, 0, 1) : averageQuality;
            var exploration = StableRandom.Unit(request.UserKey, request.NowUtc, unit.Id);
            return new ScoredItem
            {
                ItemId = unit.Id,
                RawTaste = breakdown.Total,
                Taste = taste,
                Quality = quality,
                Exploration = exploration,
                Penalized = penalized,
                Score = (wTaste * taste) + (wQuality * quality) + (wExploration * exploration),
                Explanation = Explain(index, breakdown, effective, settings),
            };
        }

        var scored = new List<ScoredItem>();
        var vectors = new Dictionary<Guid, (Guid VectorOf, IReadOnlyList<string> Collections)>();
        foreach (var group in units.GroupBy(u => u.Owner))
        {
            var owner = group.Key;
            var unitScores = group.Select(u => (u.Unit, Score: ScoreUnit(u.Unit))).ToList();
            if (!IsSplitSeries(owner))
            {
                scored.Add(unitScores[0].Score);
                vectors[owner] = (owner, CollectionKeys(index, owner));
                continue;
            }

            // Series score from its seasons: 0.6 × best + 0.4 × mean (section 5.7).
            var best = unitScores.OrderByDescending(u => u.Score.Score).ThenBy(u => u.Unit.IndexNumber ?? int.MaxValue).ThenBy(u => u.Unit.Id).First();
            double Mix(Func<ScoredItem, double> pick) => (settings.SeriesBestSeasonWeight * pick(best.Score)) + (settings.SeriesAverageSeasonWeight * unitScores.Average(u => pick(u.Score)));
            var series = byId[owner];
            var firstSeason = unitScores.Select(u => u.Unit.IndexNumber).Where(n => n.HasValue && n > 0).DefaultIfEmpty(best.Unit.IndexNumber).Min();
            scored.Add(best.Score with
            {
                ItemId = owner,
                Score = Mix(s => s.Score),
                Taste = Mix(s => s.Taste),
                Quality = Mix(s => s.Quality),
                Exploration = Mix(s => s.Exploration),
                Explanation = best.Score.Explanation with
                {
                    BestSeasonNumber = best.Unit.IndexNumber,
                    StartSeasonNumber = series.IsAnthology ? best.Unit.IndexNumber : firstSeason,
                    SimilarSeason = SimilarSeason(index, best.Unit, effective, IsSeason),
                },
            });
            vectors[owner] = (best.Unit.Id, series.Collections.Select(c => Feature.Of(FeatureCategory.Collection, c).Key).ToList());
        }

        // Rule 3: diversity (MMR on the top items) and the saga cap.
        var byOwner = scored.ToDictionary(s => s.ItemId);
        var order = Diversifier.Rerank(
            scored.Select(s => new DiversityCandidate(s.ItemId, s.Score, index.Vector(vectors[s.ItemId].VectorOf), vectors[s.ItemId].Collections)).ToList(),
            settings.DiversityLambda,
            settings.DiversityPoolSize,
            settings.MaxPerCollection,
            settings.CollectionCapWindow);

        return new RecommendationResult
        {
            Items = order.Select(id => byOwner[id]).ToList(),
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

    private static bool IsSeasonKind(MediaItem item) => string.Equals(item.Kind, SeasonKind, StringComparison.Ordinal);

    private static List<UserRating> EffectiveRatings(
        IReadOnlyList<UserRating> original,
        SeriesRatingMode mode,
        Func<Guid, bool> isSeason,
        Func<Guid, bool> isSplitSeries,
        IReadOnlyDictionary<Guid, List<MediaItem>> seasonsBySeries)
    {
        switch (mode)
        {
            case SeriesRatingMode.Series:
                // Season ratings stay stored but are not used (identical to the whole-series behaviour).
                return original.Where(r => !isSeason(r.ItemId)).ToList();
            case SeriesRatingMode.Season:
                // Series ratings stay stored but are not used; seasons without a rating are left out.
                return original.Where(r => !isSplitSeries(r.ItemId)).ToList();
            default:
                // Both: a season uses its own rating, otherwise the rating of its series.
                var result = original.Where(r => !isSplitSeries(r.ItemId)).ToList();
                var ratedSeasons = result.Select(r => r.ItemId).ToHashSet();
                foreach (var seriesRating in original.Where(r => isSplitSeries(r.ItemId)))
                {
                    foreach (var season in seasonsBySeries[seriesRating.ItemId].Where(s => !ratedSeasons.Contains(s.Id)))
                    {
                        result.Add(seriesRating with { ItemId = season.Id });
                    }
                }

                return result.OrderBy(r => r.ItemId).ToList();
        }
    }

    private static List<string> CollectionKeys(FeatureIndex index, Guid id)
        => index.Features(id).Where(f => f.Category == FeatureCategory.Collection).Select(f => f.Key).ToList();

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

    private static SeasonReference? SimilarSeason(FeatureIndex index, MediaItem candidate, IReadOnlyList<UserRating> ratings, Func<Guid, bool> isSeason)
    {
        var candidateVector = index.Vector(candidate.Id);
        SeasonReference? best = null;
        foreach (var rating in ratings.Where(r => r.Value > 0 && r.IsExplicit && isSeason(r.ItemId)))
        {
            var liked = index.Item(rating.ItemId);
            if (liked is null || liked.ParentId == candidate.ParentId)
            {
                continue;
            }

            var likedVector = index.Vector(liked.Id);
            var similarity = Diversifier.Cosine(candidateVector, likedVector);
            if (similarity <= 1e-9 || (best is not null && (similarity < best.Similarity - 1e-12 || (Math.Abs(similarity - best.Similarity) <= 1e-12 && rating.Value <= best.Value))))
            {
                continue;
            }

            // The shared credit that weighs most (writers, directors and guest stars first).
            var shared = index.Features(liked.Id)
                .Where(f => candidateVector.ContainsKey(f.FullKey))
                .OrderBy(f => f.Category is FeatureCategory.Writer or FeatureCategory.Director or FeatureCategory.GuestStar or FeatureCategory.Actor ? 0 : 1)
                .ThenByDescending(f => candidateVector[f.FullKey] * likedVector[f.FullKey])
                .ThenBy(f => f.Key, StringComparer.Ordinal)
                .Select(f => (Feature?)f)
                .FirstOrDefault();

            best = new SeasonReference
            {
                ItemId = liked.Id,
                SeriesName = liked.ParentName ?? liked.Name,
                SeasonNumber = liked.IndexNumber,
                Value = rating.Value,
                Similarity = similarity,
                SharedCategory = shared?.Category,
                SharedValue = shared?.Display,
            };
        }

        return best;
    }
}
