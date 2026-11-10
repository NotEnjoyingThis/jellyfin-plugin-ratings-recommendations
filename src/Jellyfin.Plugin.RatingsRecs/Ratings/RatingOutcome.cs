namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Outcome of setting a rating.
/// </summary>
public enum RatingOutcome
{
    /// <summary>Saved.</summary>
    Ok,

    /// <summary>The value is not -3, -1, 1 or 3.</summary>
    InvalidValue,

    /// <summary>The item does not exist or the user cannot access it.</summary>
    NotFound,

    /// <summary>The item type cannot be rated (e.g. episodes).</summary>
    NotRateable,
}
