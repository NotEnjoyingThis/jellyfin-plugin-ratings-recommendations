using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Hooks;

/// <summary>
/// The status of one hook, shown on the settings page.
/// </summary>
/// <param name="Name">The hook name.</param>
/// <param name="State">The state.</param>
/// <param name="Message">A message for the administrator (what to do when not active).</param>
/// <param name="UpdatedUtc">When the state was set.</param>
public sealed record HookStatus(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("state")] HookState State,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("updatedUtc")] DateTime UpdatedUtc);
