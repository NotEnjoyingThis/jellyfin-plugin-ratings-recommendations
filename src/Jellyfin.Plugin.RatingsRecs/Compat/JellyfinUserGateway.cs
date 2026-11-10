using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.RatingsRecs.Compat;

/// <summary>
/// <see cref="IUserGateway"/> backed by <see cref="IUserManager"/> (<c>GetUsers()</c> exists from 10.11.9).
/// </summary>
public sealed class JellyfinUserGateway : IUserGateway
{
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinUserGateway"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    public JellyfinUserGateway(IUserManager userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public User? GetUser(Guid id) => _userManager.GetUserById(id);

    /// <inheritdoc />
    public IReadOnlyList<User> GetUsers() => _userManager.GetUsers().ToList();

    /// <inheritdoc />
    public bool IsAdministrator(User user) => user.HasPermission(PermissionKind.IsAdministrator);
}
