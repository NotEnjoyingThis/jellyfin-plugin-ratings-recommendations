using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// Why an item is recommended.
/// </summary>
public sealed record Explanation
{
    /// <summary>Gets the features that contributed most (at most 2).</summary>
    public IReadOnlyList<FeatureContribution> TopFeatures { get; init; } = [];

    /// <summary>Gets the liked item sharing the top feature, if any ("Because you loved X").</summary>
    public Guid? BecauseOfItemId { get; init; }

    /// <summary>Gets the name of <see cref="BecauseOfItemId"/>.</summary>
    public string? BecauseOfName { get; init; }

    /// <summary>Gets the value of the rating of <see cref="BecauseOfItemId"/>.</summary>
    public double? BecauseOfValue { get; init; }
}
