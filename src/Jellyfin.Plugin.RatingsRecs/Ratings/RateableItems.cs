using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Which items can be rated: movies always; series and seasons depending on the series rating mode
/// (docs/DESIGN.md section 4.1); episodes never.
/// </summary>
public static class RateableItems
{
    /// <summary>
    /// The item kinds that can be rated in some mode.
    /// </summary>
    public static readonly BaseItemKind[] Kinds = [BaseItemKind.Movie, BaseItemKind.Series];

    /// <summary>
    /// Gets the stored type of a movie or series (used for Favourites from other clients).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>"Movie" or "Series", or <c>null</c>.</returns>
    public static string? GetItemType(BaseItem item) => item?.GetBaseItemKind() switch
    {
        BaseItemKind.Movie => "Movie",
        BaseItemKind.Series => "Series",
        _ => null,
    };

    /// <summary>
    /// Gets the stored type of an item that can be rated in a mode.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="mode">The user's series rating mode.</param>
    /// <returns>"Movie", "Series" or "Season", or <c>null</c> if the item cannot be rated in this mode.</returns>
    public static string? GetItemType(BaseItem item, SeriesRatingMode mode) => item?.GetBaseItemKind() switch
    {
        BaseItemKind.Movie => "Movie",
        BaseItemKind.Series when mode != SeriesRatingMode.Season => "Series",
        BaseItemKind.Season when mode != SeriesRatingMode.Series => "Season",
        _ => null,
    };
}
