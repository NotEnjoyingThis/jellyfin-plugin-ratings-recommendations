using System;
using System.Collections.Generic;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Cached recommendations of one user.
/// </summary>
public sealed record UserRecommendations
{
    /// <summary>Gets when they were computed (UTC).</summary>
    public DateTime ComputedUtc { get; init; }

    /// <summary>Gets the ranked candidates.</summary>
    public IReadOnlyList<ScoredItem> Ranked { get; init; } = [];

    /// <summary>Gets the item kinds by id.</summary>
    public IReadOnlyDictionary<Guid, string> Kinds { get; init; } = new Dictionary<Guid, string>();

    /// <summary>Gets the "Rate these" items.</summary>
    public IReadOnlyList<Guid> Onboarding { get; init; } = [];

    /// <summary>Gets the number of explicit ratings at compute time.</summary>
    public int ExplicitRatingCount { get; init; }

    /// <summary>Gets a value indicating whether cold-start weights were used.</summary>
    public bool IsColdStart { get; init; }

    /// <summary>Gets the time spent in the engine.</summary>
    public TimeSpan EngineDuration { get; init; }

    /// <summary>Gets the total compute time (library read + engine).</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Gets the profile and index kept for "similar" rankings (no library read at request time).</summary>
    public EngineContext? Context { get; init; }
}
