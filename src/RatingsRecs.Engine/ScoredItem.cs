using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// A ranked candidate.
/// </summary>
public sealed record ScoredItem
{
    /// <summary>Gets the item id.</summary>
    public Guid ItemId { get; init; }

    /// <summary>Gets the final score.</summary>
    public double Score { get; init; }

    /// <summary>Gets the raw taste score (before normalisation).</summary>
    public double RawTaste { get; init; }

    /// <summary>Gets the normalised taste score (0-1, after the proximity penalty).</summary>
    public double Taste { get; init; }

    /// <summary>Gets the quality term (0-1).</summary>
    public double Quality { get; init; }

    /// <summary>Gets the daily exploration term (0-1).</summary>
    public double Exploration { get; init; }

    /// <summary>Gets a value indicating whether the proximity penalty applied.</summary>
    public bool Penalized { get; init; }

    /// <summary>Gets the explanation.</summary>
    public Explanation Explanation { get; init; } = new();
}
