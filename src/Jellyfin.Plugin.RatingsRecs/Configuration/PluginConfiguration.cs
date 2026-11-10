using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RatingsRecs.Configuration;

/// <summary>
/// Plugin configuration. Settings are added milestone by milestone (see docs/DESIGN.md, section 7.4).
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin features are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
