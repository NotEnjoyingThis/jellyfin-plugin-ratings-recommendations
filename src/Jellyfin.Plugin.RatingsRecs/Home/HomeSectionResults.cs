using System;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.RatingsRecs.Home;

/// <summary>
/// Results of the two sections registered in Home Screen Sections. Home Screen Sections creates this class
/// through dependency injection and calls the methods by name (see <see cref="HomeSectionsBridge"/>).
/// Answers come from the recommendation cache, like the REST API, with the same access rules.
/// </summary>
public class HomeSectionResults
{
    private readonly RecommendationPresenter _presenter;
    private readonly IUserGateway _users;
    private readonly IPluginConfigProvider _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeSectionResults"/> class.
    /// </summary>
    /// <param name="presenter">The presenter.</param>
    /// <param name="users">The user gateway.</param>
    /// <param name="config">The configuration.</param>
    public HomeSectionResults(RecommendationPresenter presenter, IUserGateway users, IPluginConfigProvider config)
    {
        _presenter = presenter;
        _users = users;
        _config = config;
    }

    /// <summary>
    /// "Recommended for you".
    /// </summary>
    /// <param name="payload">The section payload.</param>
    /// <returns>The items.</returns>
    public QueryResult<BaseItemDto> GetRecommended(HomeSectionPayload payload)
    {
        var user = payload is null ? null : _users.GetUser(payload.UserId);
        if (user is null)
        {
            return Empty();
        }

        var response = _presenter.Recommendations(user, Math.Max(1, _config.Current.HomeRowSize), 0, null);
        return new QueryResult<BaseItemDto>(response.Items) { TotalRecordCount = response.Items.Count };
    }

    /// <summary>
    /// "Watched, to rate": watched but not rated yet (empty when there is nothing to rate).
    /// </summary>
    /// <param name="payload">The section payload.</param>
    /// <returns>The items.</returns>
    public QueryResult<BaseItemDto> GetRateThese(HomeSectionPayload payload)
    {
        var user = payload is null ? null : _users.GetUser(payload.UserId);
        if (user is null)
        {
            return Empty();
        }

        var response = _presenter.RateThese(user, Math.Max(1, _config.Current.HomeRowSize));
        return new QueryResult<BaseItemDto>(response.Items) { TotalRecordCount = response.Items.Count };
    }

    private static QueryResult<BaseItemDto> Empty() => new([]) { TotalRecordCount = 0 };
}
