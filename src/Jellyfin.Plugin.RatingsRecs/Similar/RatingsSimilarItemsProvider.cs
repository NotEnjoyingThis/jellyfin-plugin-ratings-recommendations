#if JF12
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Similar;

/// <summary>
/// Jellyfin 12 "similar items" provider for movies and series: the native "More like this" lists (also in TV and
/// mobile apps) ranked with the user's taste. Discovered by Jellyfin through its exports scan. Answers only for the
/// libraries selected in the plugin settings; elsewhere it returns nothing and Jellyfin's own provider answers.
/// Never computes in the request: everything comes from the recommendation cache.
/// </summary>
public sealed class RatingsSimilarItemsProvider : ILocalSimilarItemsProvider
{
    private readonly RecommendationPresenter _presenter;
    private readonly ILibraryGateway _library;
    private readonly IPluginConfigProvider _config;
    private readonly HookStatusRegistry _hooks;
    private readonly ILogger<RatingsSimilarItemsProvider> _logger;
    private volatile bool _failed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingsSimilarItemsProvider"/> class.
    /// </summary>
    /// <param name="presenter">The recommendation presenter.</param>
    /// <param name="library">The library gateway.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="hooks">The hook status registry.</param>
    /// <param name="logger">The logger.</param>
    public RatingsSimilarItemsProvider(RecommendationPresenter presenter, ILibraryGateway library, IPluginConfigProvider config, HookStatusRegistry hooks, ILogger<RatingsSimilarItemsProvider> logger)
    {
        _presenter = presenter;
        _library = library;
        _config = config;
        _hooks = hooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => SimilarProviderOrder.ProviderName;

    /// <inheritdoc />
    public MetadataPluginType Type => MetadataPluginType.LocalSimilarityProvider;

    /// <inheritdoc />
    public TimeSpan? CacheDuration => null;

    /// <inheritdoc />
    public bool Supports(Type itemType) => itemType == typeof(Movie) || itemType == typeof(Series);

    /// <inheritdoc />
    public Task<IReadOnlyList<BaseItem>> GetSimilarItemsAsync(BaseItem item, SimilarItemsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<BaseItem> none = [];
        if (_failed || query.User is null)
        {
            return Task.FromResult(none);
        }

        try
        {
            var config = _config.Current;
            var enabledLibraries = (config.SimilarProviderLibraryIds ?? []).Select(Normalize).ToHashSet(StringComparer.Ordinal);
            if (!config.Enabled || enabledLibraries.Count == 0 || !_library.GetLibraryIds(item).Any(id => enabledLibraries.Contains(id.ToString("N"))))
            {
                return Task.FromResult(none);
            }

            var items = _presenter.ProviderItems(query.User, item, query.Limit ?? 50, query.ExcludeItemIds?.ToList());
            return Task.FromResult(items ?? none);
        }
        catch (Exception ex)
        {
            // Self-disable: Jellyfin's own provider keeps answering. Shown on the settings page until the next restart.
            _failed = true;
            _logger.LogError(ex, "Ratings & Recommendations: the similar items provider failed for item {ItemId} and has been switched off until the next restart", item.Id);
            _hooks.Set(SimilarProviderOrder.HookName, HookState.Failed, "The provider failed and switched itself off until the next restart: " + ex.Message);
            return Task.FromResult(none);
        }
    }

    private static string Normalize(string id) => Guid.TryParse(id, out var guid) ? guid.ToString("N") : id;
}
#endif
