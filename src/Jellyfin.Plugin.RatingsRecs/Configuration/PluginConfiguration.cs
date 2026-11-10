using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RatingsRecs.Configuration;

/// <summary>
/// Plugin configuration. Settings are added milestone by milestone (see docs/DESIGN.md, section 7.4).
/// Defaults are the values of the design document.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin features are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the display name of the "Hate it" level (-3). Empty = translated default.
    /// </summary>
    public string NameHate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the "Watchable" level (-1). Empty = translated default.
    /// </summary>
    public string NameWatchable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the "Good" level (+1). Empty = translated default.
    /// </summary>
    public string NameGood { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the "Amazing" level (+3). Empty = translated default.
    /// </summary>
    public string NameAmazing { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile value of the "Hate it" level.
    /// </summary>
    public double ValueHate { get; set; } = -3;

    /// <summary>
    /// Gets or sets the profile value of the "Watchable" level.
    /// </summary>
    public double ValueWatchable { get; set; } = -1;

    /// <summary>
    /// Gets or sets the profile value of the "Good" level.
    /// </summary>
    public double ValueGood { get; set; } = 1;

    /// <summary>
    /// Gets or sets the profile value of the "Amazing" level.
    /// </summary>
    public double ValueAmazing { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether an "Amazing" rating adds the item to Favourites.
    /// </summary>
    public bool AmazingSetsFavorite { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a Favourite set by another client (without thumbs) counts as an implicit rating.
    /// </summary>
    public bool FavoriteFromOtherClientsCounts { get; set; } = true;

    /// <summary>
    /// Gets or sets the implicit value of a Favourite set by another client without an explicit rating.
    /// </summary>
    public double ImplicitFavoriteValue { get; set; } = 3;

    /// <summary>
    /// Gets or sets the implicit value of an item watched to the end without a rating.
    /// </summary>
    public double ImplicitWatchedValue { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the implicit value of an abandoned item (progress below 20%, idle for 30+ days).
    /// </summary>
    public double ImplicitAbandonedValue { get; set; } = -0.5;
}
