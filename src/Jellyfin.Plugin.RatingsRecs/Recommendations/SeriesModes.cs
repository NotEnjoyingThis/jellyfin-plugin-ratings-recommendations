using System;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Resolves the series rating mode of a user (docs/DESIGN.md section 4.1): the user's own choice when
/// allowed and set, otherwise the server default.
/// </summary>
public static class SeriesModes
{
    /// <summary>
    /// Parses a mode name.
    /// </summary>
    /// <param name="value">"Series", "Season" or "Both" (case-insensitive).</param>
    /// <returns>The mode, or <c>null</c> when empty or unknown.</returns>
    public static SeriesRatingMode? Parse(string? value)
        => Enum.TryParse<SeriesRatingMode>(value, true, out var mode) && Enum.IsDefined(mode) ? mode : null;

    /// <summary>
    /// Gets the server default mode.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The mode ("Series" if the setting is invalid).</returns>
    public static SeriesRatingMode Default(PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Parse(config.SeriesRatingMode) ?? SeriesRatingMode.Series;
    }

    /// <summary>
    /// Gets the mode in effect for a user.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="preferences">The user's preferences.</param>
    /// <returns>The effective mode.</returns>
    public static SeriesRatingMode Effective(PluginConfiguration config, UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(preferences);
        return config.UsersCanChooseSeriesMode && Parse(preferences.SeriesMode) is SeriesRatingMode own ? own : Default(config);
    }

    /// <summary>
    /// Determines whether seasons can be rated in a mode.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns><c>true</c> for "Season" and "Both".</returns>
    public static bool SeasonsRateable(SeriesRatingMode mode) => mode != SeriesRatingMode.Series;

    /// <summary>
    /// Determines whether whole series can be rated in a mode.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns><c>true</c> for "Series" and "Both".</returns>
    public static bool SeriesRateable(SeriesRatingMode mode) => mode != SeriesRatingMode.Season;
}
