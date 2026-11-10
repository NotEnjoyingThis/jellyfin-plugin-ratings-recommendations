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
/// <see cref="ILibraryGateway"/> backed by <see cref="ILibraryManager"/>.
/// The signatures used here are identical in 10.11.9+ and 12.x (see docs/NOTES.md); version-specific
/// branches go under <c>#if JF12</c> in this folder only.
/// </summary>
public sealed class JellyfinLibraryGateway : ILibraryGateway
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinLibraryGateway"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    public JellyfinLibraryGateway(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public BaseItem? GetItem(Guid id) => _libraryManager.GetItemById(id);

    /// <inheritdoc />
    public IReadOnlyList<BaseItem> GetItems(User? user, IReadOnlyList<BaseItemKind> kinds, bool? isFavorite = null)
    {
        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = kinds.ToArray(),
            Recursive = true,
            IsFavorite = isFavorite,
        };
        return _libraryManager.GetItemList(query);
    }
}
