using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// The liked season that resembles a recommended series' best season.
/// </summary>
/// <param name="ItemId">The liked season id.</param>
/// <param name="SeriesName">Its series name.</param>
/// <param name="SeasonNumber">Its season number.</param>
/// <param name="Value">The profile value of its rating.</param>
/// <param name="SharedCategory">The main shared feature category (writer, director, guestStar, actor...).</param>
/// <param name="SharedValue">The main shared feature value.</param>
public sealed record SimilarSeasonDto(
    [property: JsonPropertyName("ItemId")] Guid ItemId,
    [property: JsonPropertyName("SeriesName")] string SeriesName,
    [property: JsonPropertyName("SeasonNumber")] int? SeasonNumber,
    [property: JsonPropertyName("Value")] double Value,
    [property: JsonPropertyName("SharedCategory")] string? SharedCategory,
    [property: JsonPropertyName("SharedValue")] string? SharedValue);
