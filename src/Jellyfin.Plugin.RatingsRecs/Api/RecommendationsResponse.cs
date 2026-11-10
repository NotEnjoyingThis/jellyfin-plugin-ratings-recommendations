using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Response of <c>GET /RatingsRecs/Recommendations</c> and <c>GET /RatingsRecs/Onboarding</c>: standard
/// <see cref="BaseItemDto"/> items (drawn as normal cards) plus one explanation per item, in the same order.
/// </summary>
public sealed record RecommendationsResponse
{
    /// <summary>Gets the items.</summary>
    [JsonPropertyName("Items")]
    public IReadOnlyList<BaseItemDto> Items { get; init; } = [];

    /// <summary>Gets the explanations, aligned with <see cref="Items"/> (empty for onboarding).</summary>
    [JsonPropertyName("Explanations")]
    public IReadOnlyList<ExplanationDto> Explanations { get; init; } = [];

    /// <summary>Gets the total number of available items.</summary>
    [JsonPropertyName("TotalRecordCount")]
    public int TotalRecordCount { get; init; }

    /// <summary>Gets the index of the first item.</summary>
    [JsonPropertyName("StartIndex")]
    public int StartIndex { get; init; }

    /// <summary>Gets "Ready", or "Computing" when the first computation has not finished yet.</summary>
    [JsonPropertyName("Status")]
    public string Status { get; init; } = "Ready";

    /// <summary>Gets when the recommendations were computed.</summary>
    [JsonPropertyName("ComputedUtc")]
    public DateTime? ComputedUtc { get; init; }

    /// <summary>Gets a value indicating whether the user is in cold start (fewer than 5 ratings).</summary>
    [JsonPropertyName("IsColdStart")]
    public bool IsColdStart { get; init; }

    /// <summary>Gets the number of explicit ratings of the user.</summary>
    [JsonPropertyName("ExplicitRatingCount")]
    public int ExplicitRatingCount { get; init; }

    /// <summary>Gets a value indicating whether the "Rate these" row should be hidden (enough ratings).</summary>
    [JsonPropertyName("Hidden")]
    public bool Hidden { get; init; }
}
