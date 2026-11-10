using System;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// What the user did with an item, read from native user data (for implicit signals).
/// </summary>
/// <param name="ItemId">The item.</param>
/// <param name="Played">Whether it was watched to the end (series: every episode).</param>
/// <param name="Progress">Progress 0-1 (movies: position / runtime; series: share of episodes watched).</param>
/// <param name="LastPlayedUtc">When it was last played.</param>
public sealed record PlaybackFact(Guid ItemId, bool Played, double Progress, DateTime? LastPlayedUtc);
