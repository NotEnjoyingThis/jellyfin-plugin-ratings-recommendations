using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Why an item is recommended, in a language-neutral form (the web client builds the sentence).
/// </summary>
public sealed record ExplanationDto
{
    /// <summary>Gets the item id.</summary>
    [JsonPropertyName("ItemId")]
    public Guid ItemId { get; init; }

    /// <summary>Gets the features that contributed most ("director": "Ridley Scott").</summary>
    [JsonPropertyName("Features")]
    public IReadOnlyList<ExplanationFeatureDto> Features { get; init; } = [];

    /// <summary>Gets the liked item that shares the main feature, if any.</summary>
    [JsonPropertyName("BecauseOf")]
    public ExplanationItemDto? BecauseOf { get; init; }

    /// <summary>
    /// Builds the DTO from an engine explanation.
    /// </summary>
    /// <param name="itemId">The recommended item.</param>
    /// <param name="explanation">The explanation.</param>
    /// <returns>The DTO.</returns>
    public static ExplanationDto From(Guid itemId, Explanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);
        return new ExplanationDto
        {
            ItemId = itemId,
            Features = explanation.TopFeatures
                .Select(f => new ExplanationFeatureDto(JsonNamingPolicy.CamelCase.ConvertName(f.Category.ToString()), f.Value))
                .ToList(),
            BecauseOf = explanation.BecauseOfItemId is Guid id
                ? new ExplanationItemDto(id, explanation.BecauseOfName ?? string.Empty, explanation.BecauseOfValue ?? 0)
                : null,
        };
    }
}
