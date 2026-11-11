using System;
using System.IO;
using Jellyfin.Plugin.RatingsRecs.Api;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Home;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using Jellyfin.Plugin.RatingsRecs.Web;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs;

/// <summary>
/// Registers the plugin services in the Jellyfin DI container.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Folder name under <c>PluginConfigurationsPath</c>: stable across plugin updates
    /// (the versioned plugin folder is replaced on every update).
    /// </summary>
    public const string DataFolderName = "Jellyfin.Plugin.RatingsRecs";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<IPluginConfigProvider, PluginConfigProvider>();
        serviceCollection.AddSingleton<SelfWriteGuard>();

        serviceCollection.AddSingleton<ILibraryGateway, JellyfinLibraryGateway>();
        serviceCollection.AddSingleton<IUserDataGateway, JellyfinUserDataGateway>();
        serviceCollection.AddSingleton<IUserGateway, JellyfinUserGateway>();

        serviceCollection.AddSingleton<IRatingStore>(sp => new RatingStore(
            Path.Combine(sp.GetRequiredService<IApplicationPaths>().PluginConfigurationsPath, DataFolderName, "ratings"),
            sp.GetRequiredService<ILogger<RatingStore>>(),
            sp.GetRequiredService<TimeProvider>()));
        serviceCollection.AddSingleton<NativeMirror>();
        serviceCollection.AddSingleton<RatingsService>();

        serviceCollection.AddHostedService<UserDataListener>();

        serviceCollection.AddSingleton<RequestUserResolver>();
        serviceCollection.AddSingleton<LibrarySnapshotBuilder>();
        serviceCollection.AddSingleton<RecommendationService>();
        serviceCollection.AddSingleton<RecomputeScheduler>();
        serviceCollection.AddHostedService(sp => sp.GetRequiredService<RecomputeScheduler>());
        serviceCollection.AddSingleton<RecommendationPresenter>();

        serviceCollection.AddSingleton<HookStatusRegistry>();
        serviceCollection.AddHostedService<WebInjector>();
        serviceCollection.AddHostedService<HomeSectionsBridge>();
        serviceCollection.AddTransient<HomeSectionResults>();
    }
}
