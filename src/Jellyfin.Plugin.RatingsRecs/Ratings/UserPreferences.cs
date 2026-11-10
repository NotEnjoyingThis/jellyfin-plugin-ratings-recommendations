using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Per-user preferences, stored in the user's rating file.
/// </summary>
public sealed record UserPreferences
{
    /// <summary>
    /// Gets the user's own series rating mode ("Series", "Season", "Both"), or <c>null</c> to follow the server default.
    /// </summary>
    [JsonPropertyName("seriesMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SeriesMode { get; init; }
}
