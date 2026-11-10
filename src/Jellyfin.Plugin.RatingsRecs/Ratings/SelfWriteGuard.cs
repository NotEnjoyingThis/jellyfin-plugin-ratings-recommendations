using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Marks the user data writes made by the plugin itself, so that the resulting
/// <c>UserDataSaved</c> events are ignored (no feedback loop). Jellyfin raises the event
/// synchronously inside <c>SaveUserData</c>, so the mark only needs to live for that call.
/// </summary>
public sealed class SelfWriteGuard
{
    private readonly ConcurrentDictionary<(Guid UserId, Guid ItemId), int> _active = new();

    /// <summary>
    /// Marks a write in progress; dispose the result when the write is done.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>A handle that clears the mark.</returns>
    public IDisposable Enter(Guid userId, Guid itemId)
    {
        var key = (userId, itemId);
        _active.AddOrUpdate(key, 1, (_, count) => count + 1);
        return new Handle(this, key);
    }

    /// <summary>
    /// Determines whether the plugin is currently writing this user's data for this item.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <returns><c>true</c> during a plugin write.</returns>
    public bool IsActive(Guid userId, Guid itemId) => _active.ContainsKey((userId, itemId));

    private void Exit((Guid UserId, Guid ItemId) key)
    {
        while (_active.TryGetValue(key, out var count))
        {
            if (count <= 1)
            {
                if (_active.TryRemove(new System.Collections.Generic.KeyValuePair<(Guid, Guid), int>(key, count)))
                {
                    return;
                }
            }
            else if (_active.TryUpdate(key, count - 1, count))
            {
                return;
            }
        }
    }

    private sealed class Handle : IDisposable
    {
        private readonly SelfWriteGuard _owner;
        private readonly (Guid UserId, Guid ItemId) _key;
        private bool _disposed;

        public Handle(SelfWriteGuard owner, (Guid UserId, Guid ItemId) key)
        {
            _owner = owner;
            _key = key;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.Exit(_key);
        }
    }
}
