using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using MediaBrowser.Model.Plugins;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Override;

/// <summary>
/// Startup check of the optional 10.11 override (registered only in the 10.11 build): the hook is Active only when
/// the option is on and both Jellyfin endpoints exist with the expected names; re-evaluated when settings are saved.
/// A failure seen by the filter stays until the next restart.
/// </summary>
public sealed class SuggestionsOverrideStatus : IHostedService
{
    private readonly IActionDescriptorCollectionProvider _actions;
    private readonly IPluginConfigProvider _config;
    private readonly HookStatusRegistry _hooks;
    private readonly ILogger<SuggestionsOverrideStatus> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuggestionsOverrideStatus"/> class.
    /// </summary>
    /// <param name="actions">The MVC action descriptors.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="hooks">The hook status registry.</param>
    /// <param name="logger">The logger.</param>
    public SuggestionsOverrideStatus(IActionDescriptorCollectionProvider actions, IPluginConfigProvider config, HookStatusRegistry hooks, ILogger<SuggestionsOverrideStatus> logger)
    {
        _actions = actions;
        _config = config;
        _hooks = hooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance is { } plugin)
        {
            plugin.ConfigurationChanged += OnConfigurationChanged;
        }

        Evaluate();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance is { } plugin)
        {
            plugin.ConfigurationChanged -= OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e) => Evaluate();

    private void Evaluate()
    {
        if (_hooks.Get(SuggestionsOverride.HookName)?.State == HookState.Failed)
        {
            return;
        }

        if (!_config.Current.OverrideSuggestions)
        {
            _hooks.Set(SuggestionsOverride.HookName, HookState.Disabled, "Off (default): Jellyfin's own suggestions are used.");
            return;
        }

        try
        {
            var targets = _actions.ActionDescriptors.Items
                .OfType<ControllerActionDescriptor>()
                .Select(d => SuggestionsOverride.Match(d.ControllerName, d.ActionName))
                .Where(t => t != OverrideTarget.None)
                .ToHashSet();
            if (targets.Contains(OverrideTarget.Suggestions) && targets.Contains(OverrideTarget.MovieRecommendations))
            {
                _hooks.Set(SuggestionsOverride.HookName, HookState.Active, "/Items/Suggestions and /Movies/Recommendations use the user's taste.");
            }
            else
            {
                _logger.LogWarning("Ratings & Recommendations: the 10.11 suggestions override is on but the Jellyfin endpoints were not found; it stays off");
                _hooks.Set(SuggestionsOverride.HookName, HookState.Unavailable, "Jellyfin endpoints not found in this version: the override stays off.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ratings & Recommendations: could not check the endpoints for the 10.11 suggestions override");
            _hooks.Set(SuggestionsOverride.HookName, HookState.Failed, "Startup check failed: " + ex.Message);
        }
    }
}
