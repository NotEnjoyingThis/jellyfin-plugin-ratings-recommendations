using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Body of <c>PUT /RatingsRecs/Ratings/{itemId}</c>: <c>{ "value": -3 }</c>.
/// </summary>
public sealed class SetRatingRequest
{
    /// <summary>
    /// Gets or sets the level (-3, -1, 1, 3).
    /// </summary>
    [JsonPropertyName("value")]
    public int? Value { get; set; }
}
