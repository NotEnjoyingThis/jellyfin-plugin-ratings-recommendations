using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.RatingsRecs.Compat;

/// <summary>
/// User access isolated from Jellyfin version differences.
/// </summary>
public interface IUserGateway
{
    /// <summary>
    /// Gets a user by id.
    /// </summary>
    /// <param name="id">The user id.</param>
    /// <returns>The user or <c>null</c>.</returns>
    User? GetUser(Guid id);

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <returns>The users.</returns>
    IReadOnlyList<User> GetUsers();

    /// <summary>
    /// Determines whether the user is an administrator.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns><c>true</c> for administrators.</returns>
    bool IsAdministrator(User user);
}
