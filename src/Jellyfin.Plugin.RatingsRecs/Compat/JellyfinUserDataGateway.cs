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
/// <see cref="IUserDataGateway"/> backed by <see cref="IUserDataManager"/>.
/// </summary>
public sealed class JellyfinUserDataGateway : IUserDataGateway
{
    private readonly IUserDataManager _userDataManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinUserDataGateway"/> class.
    /// </summary>
    /// <param name="userDataManager">The user data manager.</param>
    public JellyfinUserDataGateway(IUserDataManager userDataManager)
    {
        _userDataManager = userDataManager;
    }

    /// <inheritdoc />
    public UserItemData? Get(User user, BaseItem item) => _userDataManager.GetUserData(user, item);

    /// <inheritdoc />
    public void Save(User user, BaseItem item, UserItemData data, UserDataSaveReason reason)
        => _userDataManager.SaveUserData(user, item, data, reason, CancellationToken.None);
}
