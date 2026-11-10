using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// The liked item an explanation refers to ("Because you loved Alien").
/// </summary>
/// <param name="ItemId">The item id.</param>
/// <param name="Name">The item name.</param>
/// <param name="Value">The profile value of its rating.</param>
public sealed record ExplanationItemDto(
    [property: JsonPropertyName("ItemId")] Guid ItemId,
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("Value")] double Value);
