using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// One rating of one user on one item.
/// </summary>
public sealed record RatingEntry
{
    /// <summary>
    /// Gets the rated item id.
    /// </summary>
    [JsonPropertyName("itemId")]
    public Guid ItemId { get; init; }

    /// <summary>
    /// Gets the item type ("Movie", "Series" or "Season").
    /// </summary>
    [JsonPropertyName("itemType")]
    public string ItemType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the canonical level (-3, -1, 1, 3).
    /// </summary>
    [JsonPropertyName("value")]
    public int Value { get; init; }

    /// <summary>
    /// Gets the source: <see cref="RatingSources.Explicit"/> or <see cref="RatingSources.Implicit"/>.
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = RatingSources.Explicit;

    /// <summary>
    /// Gets the creation time (UTC).
    /// </summary>
    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; init; }

    /// <summary>
    /// Gets the last update time (UTC).
    /// </summary>
    [JsonPropertyName("updatedUtc")]
    public DateTime UpdatedUtc { get; init; }

    /// <summary>
    /// Gets the series id, only for seasons.
    /// </summary>
    [JsonPropertyName("seriesId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? SeriesId { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is an explicit rating.
    /// </summary>
    [JsonIgnore]
    public bool IsExplicit => string.Equals(Source, RatingSources.Explicit, StringComparison.Ordinal);
}
