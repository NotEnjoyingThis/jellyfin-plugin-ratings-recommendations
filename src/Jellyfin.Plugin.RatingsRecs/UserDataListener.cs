using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs;

/// <summary>
/// Listens to <c>IUserDataManager.UserDataSaved</c> to catch Favourites set by other clients.
/// Events caused by the plugin's own writes are ignored through <see cref="SelfWriteGuard"/>.
/// Watched/abandoned signals are derived from user data when recommendations are computed.
/// </summary>
public sealed class UserDataListener : IHostedService
{
    private readonly IUserDataManager _userDataManager;
    private readonly IRatingStore _store;
    private readonly SelfWriteGuard _guard;
    private readonly IPluginConfigProvider _config;
    private readonly RecomputeScheduler _scheduler;
    private readonly ILogger<UserDataListener> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDataListener"/> class.
    /// </summary>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="store">The rating store.</param>
    /// <param name="guard">The self-write guard.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="scheduler">The recompute scheduler.</param>
    /// <param name="logger">The logger.</param>
    public UserDataListener(IUserDataManager userDataManager, IRatingStore store, SelfWriteGuard guard, IPluginConfigProvider config, RecomputeScheduler scheduler, ILogger<UserDataListener> logger)
    {
        _userDataManager = userDataManager;
        _store = store;
        _guard = guard;
        _config = config;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        _logger.LogInformation("Ratings & Recommendations: listening to user data changes");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        return Task.CompletedTask;
    }

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        try
        {
            if (e?.Item is null || e.UserData is null)
            {
                return;
            }

            // Something was watched (or marked watched/unwatched): "Rate these" and the implicit signals change.
            if (e.SaveReason is UserDataSaveReason.PlaybackFinished or UserDataSaveReason.TogglePlayed)
            {
                _scheduler.ScheduleUser(e.UserId, RecomputeScheduler.RatingDebounce);
                return;
            }

            if (e.SaveReason != UserDataSaveReason.UpdateUserRating)
            {
                return;
            }

            if (_guard.IsActive(e.UserId, e.Item.Id))
            {
                return;
            }

            var itemType = RateableItems.GetItemType(e.Item);
            if (itemType is null)
            {
                return;
            }

            if (ImplicitFavoriteRule.Apply(_store, e.UserId, e.Item.Id, itemType, e.UserData.IsFavorite, _config.Current.FavoriteFromOtherClientsCounts))
            {
                _logger.LogDebug("Implicit favourite rating updated for user {UserId}, item {ItemId}", e.UserId, e.Item.Id);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Failed to process a user data change");
        }
    }
}
