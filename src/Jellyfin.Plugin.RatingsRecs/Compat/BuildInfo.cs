using System.Linq;
using System.Reflection;

namespace Jellyfin.Plugin.RatingsRecs.Compat;

/// <summary>
/// Describes which Jellyfin ABI this assembly was compiled for.
/// Together with the gateway implementations, this is the only place allowed to use
/// the <c>JF10_11</c>/<c>JF12</c> compilation symbols.
/// </summary>
internal static class BuildInfo
{
    /// <summary>
    /// Gets the Jellyfin line this build targets ("10.11" or "12").
    /// </summary>
#if JF12
    public static string JellyfinLine => "12";
#else
    public static string JellyfinLine => "10.11";
#endif

    /// <summary>
    /// Gets the declared target ABI, as stamped by the project file.
    /// </summary>
    public static string TargetAbi { get; } = typeof(BuildInfo).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "TargetAbi")?.Value ?? "unknown";
}
