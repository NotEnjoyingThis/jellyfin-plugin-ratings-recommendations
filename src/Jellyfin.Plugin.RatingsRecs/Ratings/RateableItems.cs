using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Which items can be rated. Seasons are added with the per-season mode (milestone M3b); episodes never.
/// </summary>
public static class RateableItems
{
    /// <summary>
    /// The item kinds that can be rated today.
    /// </summary>
    public static readonly BaseItemKind[] Kinds = [BaseItemKind.Movie, BaseItemKind.Series];

    /// <summary>
    /// Gets the stored item type of a rateable item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>"Movie" or "Series", or <c>null</c> if the item cannot be rated.</returns>
    public static string? GetItemType(BaseItem item) => item?.GetBaseItemKind() switch
    {
        BaseItemKind.Movie => "Movie",
        BaseItemKind.Series => "Series",
        _ => null,
    };
}
