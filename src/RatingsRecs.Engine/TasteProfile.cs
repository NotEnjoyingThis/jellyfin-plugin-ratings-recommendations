using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// The taste profile of section 5.2: P(f) = Σ v·d(t) / (Σ d(t) + k), with d(t) = 0.5^(days / half-life).
/// </summary>
public sealed class TasteProfile
{
    private readonly Dictionary<Feature, double> _weights;

    private TasteProfile(Dictionary<Feature, double> weights)
    {
        _weights = weights;
    }

    /// <summary>
    /// Gets the number of features in the profile.
    /// </summary>
    public int Count => _weights.Count;

    /// <summary>
    /// Builds the profile.
    /// </summary>
    /// <param name="index">The feature index.</param>
    /// <param name="ratings">The ratings and signals.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The profile.</returns>
    public static TasteProfile Build(FeatureIndex index, IEnumerable<UserRating> ratings, DateTime nowUtc, EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(ratings);
        ArgumentNullException.ThrowIfNull(settings);
        var numerator = new Dictionary<Feature, double>();
        var denominator = new Dictionary<Feature, double>();
        foreach (var rating in ratings.OrderBy(r => r.ItemId))
        {
            var decay = Decay(rating.TimestampUtc, nowUtc, settings.HalfLifeDays);
            foreach (var feature in index.Features(rating.ItemId))
            {
                numerator[feature] = numerator.GetValueOrDefault(feature) + (rating.Value * decay);
                denominator[feature] = denominator.GetValueOrDefault(feature) + decay;
            }
        }

        var weights = new Dictionary<Feature, double>();
        foreach (var (feature, sum) in numerator)
        {
            var den = denominator[feature] + settings.SmoothingK;
            weights[feature] = den > 0 ? sum / den : 0;
        }

        return new TasteProfile(weights);
    }

    /// <summary>
    /// Gets d(t) = 0.5^(days / half-life); 1 when the half-life is 0 or the date is in the future.
    /// </summary>
    /// <param name="timestampUtc">The rating time.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="halfLifeDays">The half-life in days.</param>
    /// <returns>The decay factor.</returns>
    public static double Decay(DateTime timestampUtc, DateTime nowUtc, double halfLifeDays)
    {
        if (halfLifeDays <= 0)
        {
            return 1;
        }

        var days = Math.Max(0, (nowUtc - timestampUtc).TotalDays);
        return Math.Pow(0.5, days / halfLifeDays);
    }

    /// <summary>
    /// Gets P(f).
    /// </summary>
    /// <param name="feature">The feature.</param>
    /// <returns>The weight, 0 if unknown.</returns>
    public double Weight(Feature feature) => _weights.GetValueOrDefault(feature);

    /// <summary>
    /// Gets P(f) of a category and raw value.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The weight.</returns>
    public double Weight(FeatureCategory category, string value) => Weight(Feature.Of(category, value));
}
