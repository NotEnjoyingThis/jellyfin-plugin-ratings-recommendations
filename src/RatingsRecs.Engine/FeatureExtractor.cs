using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// Turns an item into its features with the limits of section 5.1.
/// </summary>
public static class FeatureExtractor
{
    /// <summary>
    /// Extracts the features of an item (distinct, in a stable order).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="settings">The settings (limits).</param>
    /// <returns>The features.</returns>
    public static IReadOnlyList<Feature> Extract(MediaItem item, EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(settings);
        var features = new List<Feature>();
        var seen = new HashSet<Feature>();

        void Add(FeatureCategory category, IEnumerable<string>? values, int limit)
        {
            if (values is null)
            {
                return;
            }

            var taken = 0;
            foreach (var value in values)
            {
                if (taken >= limit)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var feature = Feature.Of(category, value);
                if (seen.Add(feature))
                {
                    features.Add(feature);
                    taken++;
                }
            }
        }

        Add(FeatureCategory.Genre, item.Genres, int.MaxValue);
        Add(FeatureCategory.Director, item.Directors, int.MaxValue);
        Add(FeatureCategory.Actor, item.Actors, settings.MaxActors);
        Add(FeatureCategory.Writer, item.Writers, string.Equals(item.Kind, "Season", StringComparison.Ordinal) ? int.MaxValue : settings.MaxWriters);
        Add(FeatureCategory.Tag, item.Tags, int.MaxValue);
        Add(FeatureCategory.Studio, item.Studios, settings.MaxStudios);
        if (item.Year is int year && year > 0)
        {
            Add(FeatureCategory.Decade, [(year / 10 * 10).ToString(CultureInfo.InvariantCulture) + "s"], 1);
        }

        Add(FeatureCategory.Collection, item.Collections, int.MaxValue);
        Add(FeatureCategory.GuestStar, item.GuestStars, settings.MaxGuestStars);
        return features;
    }

    /// <summary>
    /// Groups features by category.
    /// </summary>
    /// <param name="features">The features.</param>
    /// <returns>Features by category.</returns>
    public static IEnumerable<IGrouping<FeatureCategory, Feature>> ByCategory(IEnumerable<Feature> features) => features.GroupBy(f => f.Category);
}
