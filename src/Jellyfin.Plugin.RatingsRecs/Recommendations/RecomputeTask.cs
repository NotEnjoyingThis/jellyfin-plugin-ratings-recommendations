using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Scheduled task: recomputes every user's recommendations (every 6 hours by default; visible and
/// runnable from Dashboard → Scheduled Tasks).
/// </summary>
public sealed class RecomputeTask : IScheduledTask
{
    private readonly RecommendationService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecomputeTask"/> class.
    /// </summary>
    /// <param name="service">The recommendation service.</param>
    public RecomputeTask(RecommendationService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Recompute recommendations";

    /// <inheritdoc />
    public string Key => "RatingsRecsRecompute";

    /// <inheritdoc />
    public string Description => "Recomputes the personal recommendations of every user (Ratings & Recommendations).";

    /// <inheritdoc />
    public string Category => "Ratings & Recommendations";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _service.ComputeAllAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(6).Ticks,
        };
    }
}
