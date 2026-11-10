using System;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// A Favourite toggled by a client without thumbs (e.g. a TV app) becomes an implicit +3,
/// and un-favouriting removes it. An explicit rating always wins and is never touched.
/// </summary>
public static class ImplicitFavoriteRule
{
    /// <summary>
    /// Applies the rule after a favourite change made outside the plugin.
    /// </summary>
    /// <param name="store">The rating store.</param>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="itemType">The stored item type.</param>
    /// <param name="isFavorite">The new favourite flag.</param>
    /// <param name="enabled">Whether the rule is enabled in the settings.</param>
    /// <returns><c>true</c> if the store changed.</returns>
    public static bool Apply(IRatingStore store, Guid userId, Guid itemId, string itemType, bool isFavorite, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!enabled)
        {
            return false;
        }

        var existing = store.Get(userId, itemId);
        if (existing is { IsExplicit: true })
        {
            return false;
        }

        if (isFavorite)
        {
            if (existing is { Value: RatingLevels.Amazing })
            {
                return false;
            }

            return store.SetImplicit(userId, itemId, itemType, RatingLevels.Amazing) is not null;
        }

        return existing is not null && store.RemoveImplicit(userId, itemId);
    }
}
