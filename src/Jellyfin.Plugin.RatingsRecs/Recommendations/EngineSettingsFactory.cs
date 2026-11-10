using System;
using System.Collections.Generic;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Maps the plugin configuration onto engine settings.
/// </summary>
public static class EngineSettingsFactory
{
    /// <summary>
    /// Builds engine settings from the configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The settings.</returns>
    public static EngineSettings From(PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var defaults = new EngineSettings();
        return defaults with
        {
            CategoryWeights = new Dictionary<FeatureCategory, double>
            {
                [FeatureCategory.Genre] = config.WeightGenre,
                [FeatureCategory.Director] = config.WeightDirector,
                [FeatureCategory.Actor] = config.WeightActor,
                [FeatureCategory.Writer] = config.WeightWriter,
                [FeatureCategory.Tag] = config.WeightTag,
                [FeatureCategory.Studio] = config.WeightStudio,
                [FeatureCategory.Decade] = config.WeightDecade,
                [FeatureCategory.Collection] = config.WeightCollection,
                [FeatureCategory.GuestStar] = defaults.Weight(FeatureCategory.GuestStar),
            },
            SmoothingK = Math.Max(0, config.SmoothingK),
            HalfLifeDays = Math.Max(0, config.HalfLifeDays),
            TasteWeight = config.TasteWeight,
            QualityWeight = config.QualityWeight,
            ExplorationWeight = config.ExplorationWeight,
            ColdStartThreshold = Math.Max(0, config.ColdStartThreshold),
            DiversityLambda = Math.Clamp(config.DiversityLambda, 0, 1),
            ProximityPenalty = Math.Clamp(config.ProximityPenalty, 0, 1),
            ExcludeHatedCollections = config.ExcludeHatedCollections,
            SeriesBestSeasonWeight = Math.Max(0, config.SeriesBestSeasonWeight),
            SeriesAverageSeasonWeight = Math.Max(0, config.SeriesAverageSeasonWeight),
        };
    }

    /// <summary>
    /// Gets the profile value of a canonical level.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="level">The level (-3, -1, 1, 3).</param>
    /// <returns>The value.</returns>
    public static double LevelValue(PluginConfiguration config, int level)
    {
        ArgumentNullException.ThrowIfNull(config);
        return level switch
        {
            -3 => config.ValueHate,
            -1 => config.ValueWatchable,
            1 => config.ValueGood,
            3 => config.ValueAmazing,
            _ => 0,
        };
    }
}
