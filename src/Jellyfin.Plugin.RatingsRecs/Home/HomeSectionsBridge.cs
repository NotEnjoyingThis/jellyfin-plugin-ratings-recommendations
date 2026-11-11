using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using MediaBrowser.Controller.Configuration;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.RatingsRecs.Home;

/// <summary>
/// Registers "Recommended for you" and "Rate these" with Home Screen Sections (its plugin interface, through
/// reflection; same mechanism on its 10.11 and 12 builds), and the "For you" page with Plugin Pages. The users then
/// add the sections from the Home Screen Sections settings and choose their position. If a plugin is missing or
/// incompatible, the hook switches itself off and the web script draws the rows / menu entry instead.
/// </summary>
public sealed class HomeSectionsBridge : BackgroundService
{
    /// <summary>Hook name of the Home Screen Sections registration.</summary>
    public const string HomeScreenSectionsHook = "HomeScreenSections";

    /// <summary>Hook name of the Plugin Pages registration.</summary>
    public const string PluginPagesHook = "PluginPages";

    /// <summary>Section id of "Recommended for you".</summary>
    public const string RecommendedSectionId = "RatingsRecsRecommended";

    /// <summary>Section id of "Rate these".</summary>
    public const string RateTheseSectionId = "RatingsRecsRateThese";

    private const int MaxAttempts = 12;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly HookStatusRegistry _status;
    private readonly IPluginConfigProvider _config;
    private readonly IServerConfigurationManager _serverConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeSectionsBridge"/> class.
    /// </summary>
    /// <param name="status">The hook registry.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="serverConfig">The server configuration (UI language of the section titles).</param>
    public HomeSectionsBridge(HookStatusRegistry status, IPluginConfigProvider config, IServerConfigurationManager serverConfig)
    {
        _status = status;
        _config = config;
        _serverConfig = serverConfig;
    }

    /// <summary>
    /// Builds the registration payload of a section.
    /// </summary>
    /// <param name="id">The section id.</param>
    /// <param name="displayText">The title.</param>
    /// <param name="method">The results method of <see cref="HomeSectionResults"/>.</param>
    /// <returns>The JSON payload.</returns>
    public static string SectionPayload(string id, string displayText, string method) => JsonSerializer.Serialize(new
    {
        id,
        displayText,
        limit = 1,
        additionalData = id,
        resultsAssembly = typeof(HomeSectionResults).Assembly.FullName,
        resultsClass = typeof(HomeSectionResults).FullName,
        resultsMethod = method,
    });

    /// <summary>
    /// Builds the Plugin Pages payload of the "For you" page.
    /// </summary>
    /// <param name="displayText">The menu label.</param>
    /// <returns>The JSON payload.</returns>
    public static string PagePayload(string displayText) => JsonSerializer.Serialize(new
    {
        id = "ratingsrecs-foryou",
        url = "/RatingsRecs/Web/foryou.html",
        displayText,
        icon = "thumb_up",
    });

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _status.Set(PluginPagesHook, HookState.Pending, "Waiting for Plugin Pages.");
        _status.Set(HomeScreenSectionsHook, HookState.Pending, "Waiting for Home Screen Sections.");
        var mode = _config.Current.HomeRowsMode;
        var italian = (_serverConfig.Configuration.UICulture ?? string.Empty).StartsWith("it", StringComparison.OrdinalIgnoreCase);
        var useHss = !string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase) && !string.Equals(mode, "Script", StringComparison.OrdinalIgnoreCase);
        if (!useHss)
        {
            _status.Set(HomeScreenSectionsHook, HookState.Disabled, "Home rows set to '" + mode + "' in the settings.");
        }

        bool hssDone = !useHss, pagesDone = false;
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested && !(hssDone && pagesDone); attempt++)
        {
            var last = attempt == MaxAttempts;
            if (!hssDone)
            {
                var result = PluginInterop.InvokeWithJson("Jellyfin.Plugin.HomeScreenSections", "Jellyfin.Plugin.HomeScreenSections.PluginInterface", "RegisterSection", SectionPayload(RecommendedSectionId, italian ? "Consigliati per te" : "Recommended for you", nameof(HomeSectionResults.GetRecommended)))
                    ?? PluginInterop.InvokeWithJson("Jellyfin.Plugin.HomeScreenSections", "Jellyfin.Plugin.HomeScreenSections.PluginInterface", "RegisterSection", SectionPayload(RateTheseSectionId, italian ? "Valuta questi" : "Rate these", nameof(HomeSectionResults.GetRateThese)));
                if (result is null)
                {
                    _status.Set(HomeScreenSectionsHook, HookState.Active, "Sections registered: users add \"Recommended for you\" and \"Rate these\" from the Home Screen Sections settings.");
                    hssDone = true;
                }
                else if (last)
                {
                    _status.Set(HomeScreenSectionsHook, result.Value.State, result.Value.Message + " The web script adds the home rows instead.");
                }
            }

            if (!pagesDone)
            {
                var result = PluginInterop.InvokeWithJson("Jellyfin.Plugin.PluginPages", "Jellyfin.Plugin.PluginPages.PluginInterface", "RegisterPage", PagePayload(italian ? "Per te" : "For you"));
                if (result is null)
                {
                    _status.Set(PluginPagesHook, HookState.Active, "\"For you\" page registered in Plugin Pages.");
                    pagesDone = true;
                }
                else if (last)
                {
                    _status.Set(PluginPagesHook, result.Value.State, result.Value.Message + " The side menu entry added by the web script still works.");
                }
            }

            if (!(hssDone && pagesDone) && !last)
            {
                try
                {
                    await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }
    }
}
