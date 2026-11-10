using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// The user's preferences as returned by <c>GET /RatingsRecs/Preferences</c>.
/// </summary>
public sealed record PreferencesDto
{
    /// <summary>Gets the series rating mode in effect: "Series", "Season" or "Both".</summary>
    [JsonPropertyName("seriesMode")]
    public string SeriesMode { get; init; } = "Series";

    /// <summary>Gets the user's own choice, or <c>null</c> when following the server default.</summary>
    [JsonPropertyName("userSeriesMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? UserSeriesMode { get; init; }

    /// <summary>Gets the server default mode.</summary>
    [JsonPropertyName("defaultSeriesMode")]
    public string DefaultSeriesMode { get; init; } = "Series";

    /// <summary>Gets a value indicating whether the user may choose a mode.</summary>
    [JsonPropertyName("canChooseSeriesMode")]
    public bool CanChooseSeriesMode { get; init; }
}
