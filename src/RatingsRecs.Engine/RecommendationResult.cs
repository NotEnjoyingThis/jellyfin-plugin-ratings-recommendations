using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// Engine output.
/// </summary>
public sealed record RecommendationResult
{
    /// <summary>Gets the candidates, best first (diversified).</summary>
    public IReadOnlyList<ScoredItem> Items { get; init; } = [];

    /// <summary>Gets a value indicating whether cold-start weights were used.</summary>
    public bool IsColdStart { get; init; }

    /// <summary>Gets the number of explicit ratings.</summary>
    public int ExplicitRatingCount { get; init; }
}
