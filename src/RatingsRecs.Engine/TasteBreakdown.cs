using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// Result of <see cref="TasteScorer.Score"/>.
/// </summary>
/// <param name="Total">The raw taste score.</param>
/// <param name="Contributions">Per-feature contributions.</param>
public sealed record TasteBreakdown(double Total, IReadOnlyList<(Feature Feature, double Contribution)> Contributions)
{
    /// <summary>
    /// Gets the features with the largest positive contributions.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <returns>The top contributions.</returns>
    public IReadOnlyList<(Feature Feature, double Contribution)> Top(int count) => Contributions
        .Where(c => c.Contribution > 1e-9)
        .OrderByDescending(c => c.Contribution)
        .ThenBy(c => c.Feature.Category)
        .ThenBy(c => c.Feature.Key, StringComparer.Ordinal)
        .Take(count)
        .ToList();
}
