using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.RatingsRecs.Similar;

/// <summary>
/// Position of our provider in a library's "similar items" provider order (Jellyfin 12).
/// Jellyfin always runs every local similar-items provider, one after the other in the configured order, and stops
/// as soon as it has enough items: ours only has an effect when it comes first. See docs/NOTES.md (M5).
/// </summary>
public static class SimilarProviderOrder
{
    /// <summary>The provider name shown in the library settings and stored in the order.</summary>
    public const string ProviderName = "Ratings & Recommendations";

    /// <summary>The hook name on the status page.</summary>
    public const string HookName = "SimilarProvider";

    /// <summary>
    /// Computes the new order for one item type of a library.
    /// Jellyfin orders by <c>SimilarItemProviderOrder</c>, or by <c>SimilarItemProviders</c> when the order is empty.
    /// </summary>
    /// <param name="order">The current <c>SimilarItemProviderOrder</c>.</param>
    /// <param name="enabledProviders">The current <c>SimilarItemProviders</c>.</param>
    /// <param name="enable">Whether our provider must be first (<c>true</c>) or absent (<c>false</c>).</param>
    /// <returns>The new order, or <c>null</c> when nothing has to change.</returns>
    public static string[]? Apply(IReadOnlyList<string>? order, IReadOnlyList<string>? enabledProviders, bool enable)
    {
        order ??= [];
        var current = order.Count > 0 ? order : (enabledProviders ?? []);
        var others = current.Where(n => !IsOurs(n)).ToArray();
        if (enable)
        {
            if (order.Count > 0 && IsOurs(order[0]) && order.Skip(1).SequenceEqual(others, StringComparer.Ordinal))
            {
                return null;
            }

            return [ProviderName, .. others];
        }

        return order.Any(IsOurs) ? order.Where(n => !IsOurs(n)).ToArray() : null;
    }

    /// <summary>
    /// The item types our provider serves in a library of the given type.
    /// </summary>
    /// <param name="collectionType">The library type (<c>null</c> = mixed content).</param>
    /// <returns>"Movie" and/or "Series".</returns>
    public static IReadOnlyList<string> ItemTypes(CollectionTypeOptions? collectionType) => collectionType switch
    {
        CollectionTypeOptions.movies => ["Movie"],
        CollectionTypeOptions.tvshows => ["Series"],
        CollectionTypeOptions.mixed or null => ["Movie", "Series"],
        _ => [],
    };

    private static bool IsOurs(string name) => string.Equals(name, ProviderName, StringComparison.OrdinalIgnoreCase);
}
