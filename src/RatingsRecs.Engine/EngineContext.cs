using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// The profile and feature index of a user, kept after a background computation so that "similar"
/// rankings can be answered quickly without reading the library again. It uses the whole-series view
/// (movies and series; seasons are not used).
/// </summary>
public sealed class EngineContext
{
    private EngineContext(RecommendationRequest request, FeatureIndex index, TasteProfile profile, IReadOnlyList<UserRating> ratings, bool coldStart)
    {
        Request = request;
        Index = index;
        Profile = profile;
        Ratings = ratings;
        IsColdStart = coldStart;
        HatedFeatures = RecommendationEngine.HatedFeatures(index, ratings);
        AverageQuality = RecommendationEngine.AverageQuality(index.Items);
    }

    /// <summary>Gets the request the context was built from.</summary>
    public RecommendationRequest Request { get; }

    /// <summary>Gets the feature index.</summary>
    public FeatureIndex Index { get; }

    /// <summary>Gets the taste profile.</summary>
    public TasteProfile Profile { get; }

    /// <summary>Gets the ratings used (one per item, seasons excluded).</summary>
    public IReadOnlyList<UserRating> Ratings { get; }

    /// <summary>Gets a value indicating whether the user is in cold start.</summary>
    public bool IsColdStart { get; }

    internal HashSet<Feature> HatedFeatures { get; }

    internal double AverageQuality { get; }

    /// <summary>
    /// Builds the context.
    /// </summary>
    /// <param name="request">The request (the series mode is ignored: seasons are left out).</param>
    /// <returns>The context.</returns>
    public static EngineContext Create(RecommendationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var seasonIds = request.Library.Where(i => string.Equals(i.Kind, "Season", StringComparison.Ordinal)).Select(i => i.Id).ToHashSet();
        var library = request.Library.Where(i => !seasonIds.Contains(i.Id)).ToList();
        var index = new FeatureIndex(library, request.Settings);
        var ratings = RecommendationEngine.Normalize(request.Ratings).Where(r => !seasonIds.Contains(r.ItemId)).ToList();
        var explicitCount = ratings.Count(r => r.IsExplicit);
        var profile = TasteProfile.Build(index, ratings, request.NowUtc, request.Settings);
        return new EngineContext(request with { Library = library }, index, profile, ratings, explicitCount < request.Settings.ColdStartThreshold);
    }
}
