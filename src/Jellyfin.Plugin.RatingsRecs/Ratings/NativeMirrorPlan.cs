namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// What to write onto native user data for a rating (docs/DESIGN.md, section 4).
/// </summary>
/// <param name="Rating">The native <c>Rating</c> (0-10), or <c>null</c> to clear it (which also clears <c>Likes</c>).</param>
/// <param name="IsFavorite">The new favourite flag, or <c>null</c> to leave it unchanged.</param>
public sealed record NativeMirrorPlan(double? Rating, bool? IsFavorite)
{
    /// <summary>
    /// Computes the plan for a level.
    /// </summary>
    /// <param name="level">The canonical level, or <c>null</c> for "no rating".</param>
    /// <param name="amazingSetsFavorite">Whether +3 adds the item to Favourites.</param>
    /// <returns>The plan.</returns>
    public static NativeMirrorPlan For(int? level, bool amazingSetsFavorite) => level switch
    {
        null => new NativeMirrorPlan(null, null),
        RatingLevels.Hate => new NativeMirrorPlan(RatingLevels.ToNativeRating(RatingLevels.Hate), false),
        RatingLevels.Watchable => new NativeMirrorPlan(RatingLevels.ToNativeRating(RatingLevels.Watchable), false),
        RatingLevels.Good => new NativeMirrorPlan(RatingLevels.ToNativeRating(RatingLevels.Good), null),
        RatingLevels.Amazing => new NativeMirrorPlan(RatingLevels.ToNativeRating(RatingLevels.Amazing), amazingSetsFavorite ? true : null),
        _ => throw new System.ArgumentOutOfRangeException(nameof(level), level, "Not a rating level."),
    };
}
