using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.RatingsRecs.Compat;

/// <summary>
/// Library access isolated from Jellyfin version differences.
/// </summary>
public interface ILibraryGateway
{
    /// <summary>
    /// Gets an item by id.
    /// </summary>
    /// <param name="id">The item id.</param>
    /// <returns>The item or <c>null</c>.</returns>
    BaseItem? GetItem(Guid id);

    /// <summary>
    /// Gets items of the given kinds, optionally restricted to what a user can access.
    /// </summary>
    /// <param name="user">The user whose library access and parental control apply, or <c>null</c> for all items.</param>
    /// <param name="kinds">Item kinds.</param>
    /// <param name="isFavorite">Optional favourite filter (requires <paramref name="user"/>).</param>
    /// <returns>The items.</returns>
    IReadOnlyList<BaseItem> GetItems(User? user, IReadOnlyList<BaseItemKind> kinds, bool? isFavorite = null);
}
