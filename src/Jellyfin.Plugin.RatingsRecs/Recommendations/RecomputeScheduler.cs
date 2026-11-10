using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Triggers background recomputation: 30 s after the last rating change of a user (debounce),
/// after a library scan, shortly after start-up, and on demand when a user has no cache yet.
/// </summary>
public sealed class RecomputeScheduler : IHostedService, IDisposable
{
    /// <summary>
    /// Debounce after a rating change (docs/DESIGN.md: 30 seconds).
    /// </summary>
    public static readonly TimeSpan RatingDebounce = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan LibraryScanDebounce = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<Guid, Timer> _userTimers = new();
    private readonly RecommendationService _service;
    private readonly IRatingStore _store;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<RecomputeScheduler> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private Timer? _allTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecomputeScheduler"/> class.
    /// </summary>
    /// <param name="service">The recommendation service.</param>
    /// <param name="store">The rating store.</param>
    /// <param name="taskManager">The task manager.</param>
    /// <param name="logger">The logger.</param>
    public RecomputeScheduler(RecommendationService service, IRatingStore store, ITaskManager taskManager, ILogger<RecomputeScheduler> logger)
    {
        _service = service;
        _store = store;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _store.RatingsChanged += OnRatingsChanged;
        _taskManager.TaskCompleted += OnTaskCompleted;
        if (Plugin.Instance is { } plugin)
        {
            // Settings saved by the administrator (weights, series mode...): recompute everybody shortly after.
            plugin.ConfigurationChanged += OnConfigurationChanged;
        }

        ScheduleAll(StartupDelay);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _store.RatingsChanged -= OnRatingsChanged;
        _taskManager.TaskCompleted -= OnTaskCompleted;
        if (Plugin.Instance is { } plugin)
        {
            plugin.ConfigurationChanged -= OnConfigurationChanged;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Schedules a recomputation of one user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="delay">The delay; a new request within the delay restarts it.</param>
    public void ScheduleUser(Guid userId, TimeSpan delay)
    {
        var timer = _userTimers.GetOrAdd(userId, id => new Timer(_ => Run(() => _service.ComputeAsync(id, _stopping.Token)), null, Timeout.Infinite, Timeout.Infinite));
        timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Requests a computation for a user without cache, unless one is already pending.
    /// </summary>
    /// <param name="userId">The user.</param>
    public void EnsureComputed(Guid userId)
    {
        if (_service.Get(userId) is null && !_userTimers.ContainsKey(userId))
        {
            ScheduleUser(userId, TimeSpan.Zero);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var timer in _userTimers.Values)
        {
            timer.Dispose();
        }

        _allTimer?.Dispose();
        _stopping.Dispose();
    }

    private void ScheduleAll(TimeSpan delay)
    {
        _allTimer ??= new Timer(_ => Run(() => _service.ComputeAllAsync(null, _stopping.Token)), null, Timeout.Infinite, Timeout.Infinite);
        _allTimer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private void OnRatingsChanged(object? sender, RatingsChangedEventArgs e) => ScheduleUser(e.UserId, RatingDebounce);

    private void OnConfigurationChanged(object? sender, MediaBrowser.Model.Plugins.BasePluginConfiguration e) => ScheduleAll(TimeSpan.FromSeconds(10));

    private void OnTaskCompleted(object? sender, TaskCompletionEventArgs e)
    {
        if (string.Equals(e.Task?.ScheduledTask?.Key, "RefreshLibrary", StringComparison.Ordinal))
        {
            ScheduleAll(LibraryScanDebounce);
        }
    }

    private void Run(Func<Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Server stopping.
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogError(ex, "Background recomputation failed");
            }
        });
    }
}
