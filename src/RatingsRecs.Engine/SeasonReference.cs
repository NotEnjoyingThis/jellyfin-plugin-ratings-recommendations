using System;

namespace RatingsRecs.Engine;

/// <summary>
/// A liked season that resembles the recommended one (section 5.7, explanation season–season).
/// </summary>
public sealed record SeasonReference
{
    /// <summary>Gets the liked season id.</summary>
    public Guid ItemId { get; init; }

    /// <summary>Gets the name of its series.</summary>
    public string SeriesName { get; init; } = string.Empty;

    /// <summary>Gets its season number.</summary>
    public int? SeasonNumber { get; init; }

    /// <summary>Gets the value of its rating.</summary>
    public double Value { get; init; }

    /// <summary>Gets the cosine similarity with the recommended season.</summary>
    public double Similarity { get; init; }

    /// <summary>Gets the category of the most important shared feature (e.g. Writer).</summary>
    public FeatureCategory? SharedCategory { get; init; }

    /// <summary>Gets the value of the most important shared feature (e.g. the writer's name).</summary>
    public string? SharedValue { get; init; }
}
