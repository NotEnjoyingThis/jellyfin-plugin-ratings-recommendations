using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Api;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Override;

/// <summary>
/// Optional Jellyfin 10.11 override (design section 6.6, point 5; registered only in the 10.11 build):
/// <c>/Items/Suggestions</c> for movies/series is replaced with the user's recommendations and every category of
/// <c>/Movies/Recommendations</c> is reordered with the user's taste, without negatively rated items.
/// Only successful responses are touched, only while the hook is Active; any error switches it off until the next
/// restart and Jellyfin's original response is sent.
/// </summary>
public sealed class SuggestionsOverrideFilter : IAsyncResultFilter
{
    private readonly RecommendationPresenter _presenter;
    private readonly RequestUserResolver _users;
    private readonly IPluginConfigProvider _config;
    private readonly HookStatusRegistry _hooks;
    private readonly ILogger<SuggestionsOverrideFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuggestionsOverrideFilter"/> class.
    /// </summary>
    /// <param name="presenter">The recommendation presenter.</param>
    /// <param name="users">The request user resolver.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="hooks">The hook status registry.</param>
    /// <param name="logger">The logger.</param>
    public SuggestionsOverrideFilter(RecommendationPresenter presenter, RequestUserResolver users, IPluginConfigProvider config, HookStatusRegistry hooks, ILogger<SuggestionsOverrideFilter> logger)
    {
        _presenter = presenter;
        _users = users;
        _config = config;
        _hooks = hooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var target = SuggestionsOverride.Match(descriptor?.ControllerName, descriptor?.ActionName);
        if (target != OverrideTarget.None
            && context.Result is ObjectResult { StatusCode: null or StatusCodes.Status200OK } result
            && _config.Current.Enabled
            && _config.Current.OverrideSuggestions
            && _hooks.Get(SuggestionsOverride.HookName)?.State == HookState.Active)
        {
            try
            {
                var replaced = await ReplaceAsync(context.HttpContext, target, result.Value).ConfigureAwait(false);
                if (replaced is not null)
                {
                    result.Value = replaced;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ratings & Recommendations: the 10.11 suggestions override failed and has been switched off until the next restart");
                _hooks.Set(SuggestionsOverride.HookName, HookState.Failed, "The override failed and switched itself off until the next restart: " + ex.Message);
            }
        }

        await next().ConfigureAwait(false);
    }

    private static int? QueryInt(HttpRequest request, string name)
        => int.TryParse(request.Query[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static Guid? RequestedUserId(HttpContext context)
    {
        var raw = context.Request.Query["userId"].ToString();
        if (string.IsNullOrEmpty(raw) && context.Request.RouteValues.TryGetValue("userId", out var route))
        {
            raw = Convert.ToString(route, CultureInfo.InvariantCulture);
        }

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private async Task<object?> ReplaceAsync(HttpContext context, OverrideTarget target, object? value)
    {
        var user = await _users.ResolveAsync(context, RequestedUserId(context)).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        if (target == OverrideTarget.Suggestions && value is QueryResult<BaseItemDto>)
        {
            if (!SuggestionsOverride.TryGetKind(context.Request.Query["type"].ToArray(), out var kind))
            {
                return null;
            }

            var startIndex = Math.Max(0, QueryInt(context.Request, "startIndex") ?? 0);
            var limit = QueryInt(context.Request, "limit") ?? 20;
            var response = _presenter.Recommendations(user, limit, startIndex, kind);
            return response.Status == "Computing"
                ? null
                : new QueryResult<BaseItemDto>(startIndex, response.TotalRecordCount, response.Items);
        }

        if (target == OverrideTarget.MovieRecommendations && value is IEnumerable<RecommendationDto> categories)
        {
            var list = categories.ToList();
            foreach (var category in list)
            {
                var items = category.Items ?? [];
                var order = _presenter.ReorderByTaste(user, items.Select(i => i.Id).ToList());
                if (order is null)
                {
                    return null;
                }

                var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
                category.Items = order.Select(id => byId[id]).ToArray();
            }

            return list.Where(c => c.Items is { Count: > 0 }).ToList();
        }

        return null;
    }
}
