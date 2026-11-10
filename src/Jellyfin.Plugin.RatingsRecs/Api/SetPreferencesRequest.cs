using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Body of <c>PUT /RatingsRecs/Preferences</c>: <c>{ "seriesMode": "Season" }</c>, or <c>null</c> for the server default.
/// </summary>
public sealed class SetPreferencesRequest
{
    /// <summary>
    /// Gets or sets the series rating mode ("Series", "Season", "Both") or <c>null</c>.
    /// </summary>
    [JsonPropertyName("seriesMode")]
    public string? SeriesMode { get; set; }
}
