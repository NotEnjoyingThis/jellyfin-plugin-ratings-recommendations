using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// Taste score of section 5.3: S = Σ_c α_c · Σ_{f ∈ x_c} P(f)·idf(f) / sqrt(|x_c|).
/// </summary>
public static class TasteScorer
{
    /// <summary>
    /// Scores an item and keeps the contribution of every feature.
    /// </summary>
    /// <param name="index">The feature index.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="item">The item.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The breakdown.</returns>
    public static TasteBreakdown Score(FeatureIndex index, TasteProfile profile, MediaItem item, EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(settings);
        var features = index.Item(item.Id) is null ? FeatureExtractor.Extract(item, settings) : index.Features(item.Id);
        var contributions = new List<(Feature Feature, double Contribution)>();
        var total = 0.0;
        foreach (var group in FeatureExtractor.ByCategory(features))
        {
            var root = Math.Sqrt(group.Count());
            var alpha = settings.Weight(group.Key, item.Kind);
            foreach (var feature in group)
            {
                var contribution = alpha * profile.Weight(feature) * index.Idf(feature) / root;
                contributions.Add((feature, contribution));
                total += contribution;
            }
        }

        return new TasteBreakdown(total, contributions);
    }
}
