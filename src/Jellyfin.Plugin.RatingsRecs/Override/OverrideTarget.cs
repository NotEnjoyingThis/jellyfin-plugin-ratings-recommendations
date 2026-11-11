namespace Jellyfin.Plugin.RatingsRecs.Override;

/// <summary>
/// Endpoints touched by the optional Jellyfin 10.11 override.
/// </summary>
public enum OverrideTarget
{
    /// <summary>Not overridden.</summary>
    None,

    /// <summary><c>/Items/Suggestions</c>: replaced with the user's recommendations.</summary>
    Suggestions,

    /// <summary><c>/Movies/Recommendations</c>: each category reordered with the user's taste.</summary>
    MovieRecommendations,
}
