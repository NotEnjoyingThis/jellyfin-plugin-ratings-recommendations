using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Hooks;

/// <summary>
/// Keeps the status of every hook so that failures are logged once and shown on the settings page.
/// </summary>
public sealed class HookStatusRegistry
{
    /// <summary>Hook name of the web client injection.</summary>
    public const string WebInjection = "WebInjection";

    private readonly ConcurrentDictionary<string, HookStatus> _statuses = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;
    private readonly ILogger<HookStatusRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HookStatusRegistry"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public HookStatusRegistry(TimeProvider clock, ILogger<HookStatusRegistry> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Sets the status of a hook, logging state changes.
    /// </summary>
    /// <param name="name">The hook name.</param>
    /// <param name="state">The state.</param>
    /// <param name="message">The message.</param>
    public void Set(string name, HookState state, string message)
    {
        var status = new HookStatus(name, state, message, _clock.GetUtcNow().UtcDateTime);
        var previous = _statuses.GetValueOrDefault(name);
        _statuses[name] = status;
        if (previous is not null && previous.State == state && previous.Message == message)
        {
            return;
        }

        switch (state)
        {
            case HookState.Failed:
                _logger.LogWarning("Ratings & Recommendations hook {Hook} switched off: {Message}", name, message);
                break;
            case HookState.Unavailable:
                _logger.LogInformation("Ratings & Recommendations hook {Hook} unavailable: {Message}", name, message);
                break;
            default:
                _logger.LogInformation("Ratings & Recommendations hook {Hook}: {State}. {Message}", name, state, message);
                break;
        }
    }

    /// <summary>
    /// Gets the status of a hook.
    /// </summary>
    /// <param name="name">The hook name.</param>
    /// <returns>The status, or <c>null</c>.</returns>
    public HookStatus? Get(string name) => _statuses.GetValueOrDefault(name);

    /// <summary>
    /// Gets all statuses, by name.
    /// </summary>
    /// <returns>The statuses.</returns>
    public IReadOnlyList<HookStatus> GetAll() => _statuses.Values.OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
}
