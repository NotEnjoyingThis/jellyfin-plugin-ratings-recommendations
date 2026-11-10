using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// All engine coefficients, with the defaults of docs/DESIGN.md section 5.
/// </summary>
public sealed record EngineSettings
{
    /// <summary>Gets the category weights α (section 5.1).</summary>
    public IReadOnlyDictionary<FeatureCategory, double> CategoryWeights { get; init; } = new Dictionary<FeatureCategory, double>
    {
        [FeatureCategory.Genre] = 1.0,
        [FeatureCategory.Director] = 1.2,
        [FeatureCategory.Actor] = 0.8,
        [FeatureCategory.Writer] = 0.6,
        [FeatureCategory.Tag] = 0.5,
        [FeatureCategory.Studio] = 0.3,
        [FeatureCategory.Decade] = 0.4,
        [FeatureCategory.Collection] = 1.0,
        [FeatureCategory.GuestStar] = 0.6,
    };

    /// <summary>Gets how many actors are used (cast order).</summary>
    public int MaxActors { get; init; } = 5;

    /// <summary>Gets how many writers are used.</summary>
    public int MaxWriters { get; init; } = 3;

    /// <summary>Gets how many studios are used.</summary>
    public int MaxStudios { get; init; } = 2;

    /// <summary>Gets how many guest stars are used (seasons).</summary>
    public int MaxGuestStars { get; init; } = 5;

    /// <summary>Gets a value indicating whether the IDF rarity weight is used (tests of section 5.6 disable it).</summary>
    public bool UseIdf { get; init; } = true;

    /// <summary>Gets the smoothing constant k of the profile (section 5.2).</summary>
    public double SmoothingK { get; init; } = 2;

    /// <summary>Gets the half-life of a rating in days; 0 = no decay.</summary>
    public double HalfLifeDays { get; init; } = 365;

    /// <summary>Gets the weight of the taste term.</summary>
    public double TasteWeight { get; init; } = 0.75;

    /// <summary>Gets the weight of the quality term.</summary>
    public double QualityWeight { get; init; } = 0.15;

    /// <summary>Gets the weight of the exploration term.</summary>
    public double ExplorationWeight { get; init; } = 0.10;

    /// <summary>Gets the minimum number of explicit ratings to leave the cold start (section 5.5).</summary>
    public int ColdStartThreshold { get; init; } = 5;

    /// <summary>Gets the taste weight during cold start.</summary>
    public double ColdTasteWeight { get; init; } = 0.3;

    /// <summary>Gets the quality weight during cold start.</summary>
    public double ColdQualityWeight { get; init; } = 0.5;

    /// <summary>Gets the exploration weight during cold start.</summary>
    public double ColdExplorationWeight { get; init; } = 0.2;

    /// <summary>Gets the factor applied to the taste of items sharing a director or saga with a -3 item.</summary>
    public double ProximityPenalty { get; init; } = 0.5;

    /// <summary>Gets a value indicating whether other chapters of a saga rated -3 are excluded.</summary>
    public bool ExcludeHatedCollections { get; init; } = true;

    /// <summary>Gets the MMR λ (1 = no diversification).</summary>
    public double DiversityLambda { get; init; } = 0.8;

    /// <summary>Gets how many top items are re-ranked by MMR.</summary>
    public int DiversityPoolSize { get; init; } = 50;

    /// <summary>Gets the maximum number of items of one saga in the first <see cref="CollectionCapWindow"/>.</summary>
    public int MaxPerCollection { get; init; } = 2;

    /// <summary>Gets the window of the saga cap.</summary>
    public int CollectionCapWindow { get; init; } = 20;

    /// <summary>Gets the number of features in an explanation.</summary>
    public int ExplanationFeatures { get; init; } = 2;

    /// <summary>Gets the weight of the best season in a series score (section 5.7).</summary>
    public double SeriesBestSeasonWeight { get; init; } = 0.6;

    /// <summary>Gets the weight of the average season in a series score (section 5.7).</summary>
    public double SeriesAverageSeasonWeight { get; init; } = 0.4;

    /// <summary>Gets the weight of a category, 0 if missing.</summary>
    /// <param name="category">The category.</param>
    /// <returns>α.</returns>
    public double Weight(FeatureCategory category) => CategoryWeights.TryGetValue(category, out var w) ? w : 0;
}
