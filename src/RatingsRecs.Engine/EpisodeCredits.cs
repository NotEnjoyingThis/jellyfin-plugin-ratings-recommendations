using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// Credits and rating of one episode, used to build the features of its season (section 5.7).
/// </summary>
public sealed record EpisodeCredits
{
    /// <summary>Gets the directors.</summary>
    public IReadOnlyList<string> Directors { get; init; } = [];

    /// <summary>Gets the writers.</summary>
    public IReadOnlyList<string> Writers { get; init; } = [];

    /// <summary>Gets the guest stars.</summary>
    public IReadOnlyList<string> GuestStars { get; init; } = [];

    /// <summary>Gets the community rating (0-10).</summary>
    public double? CommunityRating { get; init; }

    /// <summary>Gets the year of first broadcast.</summary>
    public int? Year { get; init; }
}
