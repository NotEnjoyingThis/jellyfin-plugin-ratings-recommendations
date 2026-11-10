namespace Jellyfin.Plugin.RatingsRecs.Configuration;

/// <summary>
/// Gives services the current configuration (testable replacement for <c>Plugin.Instance.Configuration</c>).
/// </summary>
public interface IPluginConfigProvider
{
    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    PluginConfiguration Current { get; }
}

/// <summary>
/// Reads the configuration of the running plugin instance.
/// </summary>
public sealed class PluginConfigProvider : IPluginConfigProvider
{
    /// <inheritdoc />
    public PluginConfiguration Current => Plugin.Instance?.Configuration ?? new PluginConfiguration();
}
