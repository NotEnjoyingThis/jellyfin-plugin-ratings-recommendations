using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// Engine input for one user.
/// </summary>
public sealed record RecommendationRequest
{
    /// <summary>Gets the items the user can access (movies, series; seasons in per-season mode).</summary>
    public IReadOnlyList<MediaItem> Library { get; init; } = [];

    /// <summary>Gets the user's ratings and implicit signals (one per item).</summary>
    public IReadOnlyList<UserRating> Ratings { get; init; } = [];

    /// <summary>Gets a stable per-user key, used to seed the daily exploration term.</summary>
    public string UserKey { get; init; } = string.Empty;

    /// <summary>Gets the current time (UTC): decay and the exploration day.</summary>
    public DateTime NowUtc { get; init; }

    /// <summary>Gets the settings.</summary>
    public EngineSettings Settings { get; init; } = new();

    /// <summary>Gets how series are rated; seasons (Kind "Season", ParentId = series) are used only in the per-season modes.</summary>
    public SeriesRatingMode Mode { get; init; } = SeriesRatingMode.Series;
}
