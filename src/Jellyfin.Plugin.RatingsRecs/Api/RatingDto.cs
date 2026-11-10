using System;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.RatingsRecs.Ratings;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// A rating as returned by the API. <c>value</c> is <c>null</c> when the item is not rated.
/// </summary>
public sealed record RatingDto
{
    /// <summary>
    /// Gets the item id.
    /// </summary>
    [JsonPropertyName("itemId")]
    public Guid ItemId { get; init; }

    /// <summary>
    /// Gets the level (-3, -1, 1, 3) or <c>null</c>.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? Value { get; init; }

    /// <summary>
    /// Gets the source ("explicit" or "implicit") or <c>null</c>.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>
    /// Gets the item type.
    /// </summary>
    [JsonPropertyName("itemType")]
    public string? ItemType { get; init; }

    /// <summary>
    /// Gets the creation time (UTC).
    /// </summary>
    [JsonPropertyName("createdUtc")]
    public DateTime? CreatedUtc { get; init; }

    /// <summary>
    /// Gets the last update time (UTC).
    /// </summary>
    [JsonPropertyName("updatedUtc")]
    public DateTime? UpdatedUtc { get; init; }

    /// <summary>
    /// Builds the DTO of an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="entry">The rating, or <c>null</c>.</param>
    /// <returns>The DTO.</returns>
    public static RatingDto From(Guid itemId, RatingEntry? entry) => new()
    {
        ItemId = itemId,
        Value = entry?.Value,
        Source = entry?.Source,
        ItemType = entry?.ItemType,
        CreatedUtc = entry?.CreatedUtc,
        UpdatedUtc = entry?.UpdatedUtc,
    };
}
