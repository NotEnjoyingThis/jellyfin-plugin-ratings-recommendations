using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.RatingsRecs.Compat;

/// <summary>
/// User data access isolated from Jellyfin version differences.
/// </summary>
public interface IUserDataGateway
{
    /// <summary>
    /// Gets the user data of an item.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="item">The item.</param>
    /// <returns>The user data, or <c>null</c>.</returns>
    UserItemData? Get(User user, BaseItem item);

    /// <summary>
    /// Saves the user data of an item (raises <c>IUserDataManager.UserDataSaved</c>).
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="item">The item.</param>
    /// <param name="data">The user data.</param>
    /// <param name="reason">The save reason.</param>
    void Save(User user, BaseItem item, UserItemData data, UserDataSaveReason reason);
}
