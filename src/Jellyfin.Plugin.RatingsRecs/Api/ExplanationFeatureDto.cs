using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// One feature of an explanation.
/// </summary>
/// <param name="Category">The category: genre, director, actor, writer, tag, studio, decade, collection, guestStar.</param>
/// <param name="Value">The display value.</param>
public sealed record ExplanationFeatureDto(
    [property: JsonPropertyName("Category")] string Category,
    [property: JsonPropertyName("Value")] string Value);
