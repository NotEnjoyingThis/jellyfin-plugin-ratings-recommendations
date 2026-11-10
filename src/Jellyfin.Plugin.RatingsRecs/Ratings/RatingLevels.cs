using System;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// The four canonical rating levels and their mirror onto native Jellyfin user data
/// (docs/DESIGN.md, section 4). The stored and exchanged value is always the canonical level;
/// the configurable profile value is applied only by the recommendation engine.
/// </summary>
public static class RatingLevels
{
    /// <summary>Hate it.</summary>
    public const int Hate = -3;

    /// <summary>Watchable.</summary>
    public const int Watchable = -1;

    /// <summary>Good.</summary>
    public const int Good = 1;

    /// <summary>Amazing.</summary>
    public const int Amazing = 3;

    /// <summary>
    /// Determines whether a value is one of the four canonical levels.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><c>true</c> for -3, -1, 1 and 3.</returns>
    public static bool IsValid(int value) => value is Hate or Watchable or Good or Amazing;

    /// <summary>
    /// Gets the native <c>UserItemData.Rating</c> (0-10) for a level. <c>Likes</c> is derived by Jellyfin
    /// from <c>Rating</c> (true when Rating &gt;= 6.5), so only <c>Rating</c> must be written.
    /// </summary>
    /// <param name="level">The canonical level.</param>
    /// <returns>The native rating.</returns>
    public static double ToNativeRating(int level) => level switch
    {
        Hate => 2,
        Watchable => 4,
        Good => 7,
        Amazing => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Not a rating level."),
    };
}
