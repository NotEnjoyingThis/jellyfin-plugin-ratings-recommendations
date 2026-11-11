using System.Collections.Generic;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.RatingsRecs.Hooks;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Response of <c>GET /RatingsRecs/Status</c>.
/// </summary>
public sealed record PluginStatusDto
{
    /// <summary>Gets the plugin version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>Gets the target ABI of this build.</summary>
    [JsonPropertyName("targetAbi")]
    public string TargetAbi { get; init; } = string.Empty;

    /// <summary>Gets the Jellyfin line of this build.</summary>
    [JsonPropertyName("jellyfinLine")]
    public string JellyfinLine { get; init; } = string.Empty;

    /// <summary>Gets the hook statuses.</summary>
    [JsonPropertyName("hooks")]
    public IReadOnlyList<HookStatus> Hooks { get; init; } = [];

    /// <summary>Gets the share of seasons without per-episode credits (null until the first full computation).</summary>
    [JsonPropertyName("seasonCredits")]
    public Jellyfin.Plugin.RatingsRecs.Recommendations.SeasonCreditsReport? SeasonCredits { get; init; }

    /// <summary>Gets the last time the web script was injected into index.html (null if never since start).</summary>
    [JsonPropertyName("lastInjectedUtc")]
    public System.DateTime? LastInjectedUtc { get; init; }
}
