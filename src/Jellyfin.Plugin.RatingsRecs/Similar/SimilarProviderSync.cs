#if JF12
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Similar;

/// <summary>
/// Keeps the "similar items" provider order of the libraries in line with the plugin settings (Jellyfin 12):
/// our provider first in the selected libraries, absent from the others. Runs at startup and whenever the plugin
/// settings are saved, through Jellyfin's own library options API. Libraries without options for an item type are
/// left untouched (creating them would change their metadata downloaders) and reported on the settings page.
/// </summary>
public sealed class SimilarProviderSync : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ISimilarItemsManager _similarItemsManager;
    private readonly IPluginConfigProvider _config;
    private readonly HookStatusRegistry _hooks;
    private readonly ILogger<SimilarProviderSync> _logger;
    private readonly Lock _lock = new();
    private bool _registered;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimilarProviderSync"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="similarItemsManager">The similar items manager.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="hooks">The hook status registry.</param>
    /// <param name="logger">The logger.</param>
    public SimilarProviderSync(ILibraryManager libraryManager, ISimilarItemsManager similarItemsManager, IPluginConfigProvider config, HookStatusRegistry hooks, ILogger<SimilarProviderSync> logger)
    {
        _libraryManager = libraryManager;
        _similarItemsManager = similarItemsManager;
        _config = config;
        _hooks = hooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _registered = _similarItemsManager.GetSimilarItemsProviders<Movie>().Any(p => p is RatingsSimilarItemsProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ratings & Recommendations: could not check the similar items provider registration");
        }

        if (!_registered)
        {
            _logger.LogWarning("Ratings & Recommendations: Jellyfin did not register the similar items provider; library settings are left unchanged");
            _hooks.Set(SimilarProviderOrder.HookName, HookState.Failed, "Jellyfin did not register the provider: library settings are left unchanged.");
            return Task.CompletedTask;
        }

        if (Plugin.Instance is { } plugin)
        {
            plugin.ConfigurationChanged += OnConfigurationChanged;
        }

        Sync();
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

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e) => Sync();

    private void Sync()
    {
        lock (_lock)
        {
            try
            {
                SyncLocked();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ratings & Recommendations: could not update the similar items provider order of the libraries");
                _hooks.Set(SimilarProviderOrder.HookName, HookState.Failed, "Could not update the library settings: " + ex.Message);
            }
        }
    }

    private void SyncLocked()
    {
        var config = _config.Current;
        var selected = (config.SimilarProviderLibraryIds ?? [])
            .Select(id => Guid.TryParse(id, out var guid) ? guid.ToString("N") : id)
            .ToHashSet(StringComparer.Ordinal);
        var active = new List<string>();
        var notApplied = new List<string>();
        foreach (var library in _libraryManager.GetVirtualFolders())
        {
            var types = SimilarProviderOrder.ItemTypes(library.CollectionType);
            if (types.Count == 0 || !Guid.TryParse(library.ItemId, out var libraryId) || _libraryManager.GetItemById(libraryId) is not CollectionFolder folder)
            {
                continue;
            }

            var enable = config.Enabled && selected.Contains(libraryId.ToString("N"));
            var options = folder.GetLibraryOptions();
            var changed = false;
            var missing = false;
            foreach (var type in types)
            {
                var typeOptions = options.GetTypeOptions(type);
                if (typeOptions is null)
                {
                    missing |= enable;
                    continue;
                }

                var order = SimilarProviderOrder.Apply(typeOptions.SimilarItemProviderOrder, typeOptions.SimilarItemProviders, enable);
                if (order is not null)
                {
                    typeOptions.SimilarItemProviderOrder = order;
                    changed = true;
                }
            }

            if (changed)
            {
                folder.UpdateLibraryOptions(options);
                _logger.LogInformation("Ratings & Recommendations: similar items provider {Action} in library {Library}", enable ? "placed first" : "removed", library.Name);
            }

            if (missing)
            {
                notApplied.Add(library.Name);
            }
            else if (enable)
            {
                active.Add(library.Name);
            }
        }

        if (active.Count == 0 && notApplied.Count == 0)
        {
            _hooks.Set(SimilarProviderOrder.HookName, HookState.Disabled, "No library selected: Jellyfin's own similar items are used.");
            return;
        }

        var message = active.Count > 0 ? "Active in: " + string.Join(", ", active) + "." : "Not active in any library.";
        if (notApplied.Count > 0)
        {
            message += string.Format(
                CultureInfo.InvariantCulture,
                " Not applied to {0}: open the library in Dashboard > Libraries, save it once, then save these settings again.",
                string.Join(", ", notApplied));
        }

        _hooks.Set(SimilarProviderOrder.HookName, active.Count > 0 ? HookState.Active : HookState.Unavailable, message);
    }
}
#endif
