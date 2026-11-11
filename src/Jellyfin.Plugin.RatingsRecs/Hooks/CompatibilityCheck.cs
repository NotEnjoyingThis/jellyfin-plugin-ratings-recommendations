using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Compat;
using MediaBrowser.Controller;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Hooks;

/// <summary>
/// Runs <see cref="Compatibility.Check"/> at startup and shows the result on the settings page (and in the log on failure).
/// </summary>
public sealed class CompatibilityCheck : IHostedService
{
    private readonly IServerApplicationHost _host;
    private readonly HookStatusRegistry _hooks;
    private readonly ILogger<CompatibilityCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompatibilityCheck"/> class.
    /// </summary>
    /// <param name="host">The server application host.</param>
    /// <param name="hooks">The hook status registry.</param>
    /// <param name="logger">The logger.</param>
    public CompatibilityCheck(IServerApplicationHost host, HookStatusRegistry hooks, ILogger<CompatibilityCheck> logger)
    {
        _host = host;
        _hooks = hooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var (state, message) = Compatibility.Check(BuildInfo.JellyfinLine, _host.ApplicationVersion);
        if (state != HookState.Active)
        {
            _logger.LogError("Ratings & Recommendations: {Message}", message);
        }

        _hooks.Set(Compatibility.HookName, state, message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
