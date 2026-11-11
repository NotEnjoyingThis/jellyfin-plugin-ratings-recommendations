using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.RatingsRecs.Compat;

/// <summary>
/// <see cref="ILibraryGateway"/> backed by <see cref="ILibraryManager"/>.
/// Signatures are identical in 10.11.9+ and 12.x except where marked with <c>#if JF12</c> (see docs/NOTES.md).
/// </summary>
public sealed class JellyfinLibraryGateway : ILibraryGateway
{
    private const int PeopleBatchSize = 500;

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
    public IReadOnlyList<BaseItem> GetItems(User? user, IReadOnlyList<BaseItemKind> kinds, bool? isFavorite = null, bool? isPlayed = null, bool? isResumable = null)
    {
        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = kinds.ToArray(),
            Recursive = true,
            IsFavorite = isFavorite,
            IsPlayed = isPlayed,
            IsResumable = isResumable,
        };
        return _libraryManager.GetItemList(query);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Guid, IReadOnlyList<PersonInfo>> GetPeople(IReadOnlyList<BaseItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var result = new Dictionary<Guid, IReadOnlyList<PersonInfo>>();
#if JF12
        // Jellyfin 12 reads people for many items in one query.
        for (var i = 0; i < items.Count; i += PeopleBatchSize)
        {
            var ids = items.Skip(i).Take(PeopleBatchSize).Select(item => item.Id).ToList();
            foreach (var (id, people) in _libraryManager.GetPeopleByItems(ids))
            {
                result[id] = people;
            }
        }
#else
        _ = PeopleBatchSize;
        foreach (var item in items)
        {
            result[item.Id] = _libraryManager.GetPeople(item);
        }
#endif
        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<Guid> GetLibraryIds(BaseItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _libraryManager.GetCollectionFolders(item).Select(folder => folder.Id).ToList();
    }
}
