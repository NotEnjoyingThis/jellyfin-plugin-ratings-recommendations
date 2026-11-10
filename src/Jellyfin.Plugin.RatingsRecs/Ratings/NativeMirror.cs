using System;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Copies a rating onto native <c>UserItemData</c> (<c>Rating</c>, hence <c>Likes</c>, and <c>IsFavorite</c>).
/// The plugin store stays the source of truth: a mirror failure is logged, never fatal.
/// </summary>
public sealed class NativeMirror
{
    private readonly IUserDataGateway _userData;
    private readonly SelfWriteGuard _guard;
    private readonly IPluginConfigProvider _config;
    private readonly ILogger<NativeMirror> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeMirror"/> class.
    /// </summary>
    /// <param name="userData">The user data gateway.</param>
    /// <param name="guard">The self-write guard.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public NativeMirror(IUserDataGateway userData, SelfWriteGuard guard, IPluginConfigProvider config, ILogger<NativeMirror> logger)
    {
        _userData = userData;
        _guard = guard;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Mirrors a level (or its removal) onto native user data.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="item">The item.</param>
    /// <param name="level">The level, or <c>null</c> when the rating was removed.</param>
    /// <returns><c>true</c> if the native data was written.</returns>
    public bool Apply(User user, BaseItem item, int? level)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            var data = _userData.Get(user, item);
            if (data is null)
            {
                _logger.LogWarning("No user data for item {ItemId}; native mirror skipped", item.Id);
                return false;
            }

            var plan = NativeMirrorPlan.For(level, _config.Current.AmazingSetsFavorite);
            data.Rating = plan.Rating;
            if (plan.IsFavorite is bool favorite)
            {
                data.IsFavorite = favorite;
            }

            using (_guard.Enter(user.Id, item.Id))
            {
                _userData.Save(user, item, data, UserDataSaveReason.UpdateUserRating);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Native mirror failed for item {ItemId}; the rating is kept in the plugin store", item.Id);
            return false;
        }
    }
}
