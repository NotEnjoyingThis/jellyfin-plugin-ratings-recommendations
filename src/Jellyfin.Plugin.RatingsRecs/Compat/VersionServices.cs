using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RatingsRecs.Compat;

/// <summary>
/// Services that exist only on one Jellyfin line (the only place where they are registered conditionally).
/// The Jellyfin 12 similar items provider itself is not registered here: Jellyfin finds it through its exports scan.
/// </summary>
internal static class VersionServices
{
    /// <summary>
    /// Registers the services of the Jellyfin line this assembly was built for.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void Register(IServiceCollection services)
    {
#if JF12
        services.AddHostedService<Similar.SimilarProviderSync>();
#else
        // Optional override of /Items/Suggestions and /Movies/Recommendations (off by default).
        services.AddSingleton<Override.SuggestionsOverrideFilter>();
        services.AddHostedService<Override.SuggestionsOverrideStatus>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<Override.SuggestionsOverrideFilter>());
#endif
    }
}
