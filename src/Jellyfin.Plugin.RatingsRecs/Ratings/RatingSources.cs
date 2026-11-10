namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Sources of a rating.
/// </summary>
public static class RatingSources
{
    /// <summary>
    /// Set by the user with the thumbs selector, the API, the Favourites import or a CSV import.
    /// </summary>
    public const string Explicit = "explicit";

    /// <summary>
    /// Derived from a signal (e.g. Favourite set by a client without thumbs). Never overrides an explicit rating.
    /// </summary>
    public const string Implicit = "implicit";
}
