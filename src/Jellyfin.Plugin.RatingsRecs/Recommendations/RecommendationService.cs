using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Microsoft.Extensions.Logging;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Computes recommendations in the background and keeps them in an in-memory cache per user.
/// HTTP requests only read the cache (docs/DESIGN.md sections 6.4 and 8).
/// </summary>
public sealed class RecommendationService : IDisposable
{
    /// <summary>
    /// How many "Rate these" items are kept.
    /// </summary>
    public const int OnboardingSize = 40;

    private readonly ConcurrentDictionary<Guid, UserRecommendations> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IUserGateway _users;
    private readonly IRatingStore _store;
    private readonly LibrarySnapshotBuilder _snapshots;
    private readonly IPluginConfigProvider _config;
    private readonly TimeProvider _clock;
    private readonly ILogger<RecommendationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationService"/> class.
    /// </summary>
    /// <param name="users">The user gateway.</param>
    /// <param name="store">The rating store.</param>
    /// <param name="snapshots">The library snapshot builder.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public RecommendationService(IUserGateway users, IRatingStore store, LibrarySnapshotBuilder snapshots, IPluginConfigProvider config, TimeProvider clock, ILogger<RecommendationService> logger)
    {
        _users = users;
        _store = store;
        _snapshots = snapshots;
        _config = config;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets the cached recommendations of a user, or <c>null</c> if not computed yet.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>The recommendations.</returns>
    public UserRecommendations? Get(Guid userId) => _cache.GetValueOrDefault(userId);

    /// <summary>
    /// Recomputes the recommendations of one user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ComputeAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var user = _users.GetUser(userId);
            if (user is null)
            {
                _cache.TryRemove(userId, out _);
                return;
            }

            ComputeCore(user);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Recomputes the recommendations of every user.
    /// </summary>
    /// <param name="progress">Progress (0-100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ComputeAllAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var users = _users.GetUsers();
        for (var i = 0; i < users.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ComputeAsync(users[i].Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            {
                _logger.LogError(ex, "Recommendations failed for user {UserId}", users[i].Id);
            }

            progress?.Report(100.0 * (i + 1) / users.Count);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private void ComputeCore(Jellyfin.Database.Implementations.Entities.User user)
    {
        var total = Stopwatch.StartNew();
        var config = _config.Current;
        var now = _clock.GetUtcNow().UtcDateTime;
        var snapshot = _snapshots.Build(user);
        var signals = SignalBuilder.Build(_store.GetAll(user.Id), snapshot.Facts, config, now);
        var request = new RecommendationRequest
        {
            Library = snapshot.Items,
            Ratings = signals,
            UserKey = user.Id.ToString("N", CultureInfo.InvariantCulture),
            NowUtc = now,
            Settings = EngineSettingsFactory.From(config),
        };

        var engine = Stopwatch.StartNew();
        var result = RecommendationEngine.Recommend(request);
        var onboarding = Onboarding.Select(request with { Ratings = signals.Where(s => s.IsExplicit).ToList() }, OnboardingSize);
        engine.Stop();
        total.Stop();

        _cache[user.Id] = new UserRecommendations
        {
            ComputedUtc = now,
            Ranked = result.Items,
            Kinds = snapshot.Kinds,
            Onboarding = onboarding,
            ExplicitRatingCount = result.ExplicitRatingCount,
            IsColdStart = result.IsColdStart,
            EngineDuration = engine.Elapsed,
            TotalDuration = total.Elapsed,
        };

        _logger.LogInformation(
            "Recommendations for {User}: {Count} candidates from {Library} items and {Signals} signals, engine {EngineMs} ms, total {TotalMs} ms",
            user.Username,
            result.Items.Count,
            snapshot.Items.Count,
            signals.Count,
            (long)engine.Elapsed.TotalMilliseconds,
            (long)total.Elapsed.TotalMilliseconds);
    }
}
