using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Recommendations API (docs/DESIGN.md section 6.5). Answers from the cache only; the computation runs in
/// the background (scheduled task, rating changes, library scans).
/// </summary>
[ApiController]
[Authorize]
[Route("RatingsRecs")]
[Produces(MediaTypeNames.Application.Json)]
public class RecommendationsController : ControllerBase
{
    private readonly RecommendationPresenter _presenter;
    private readonly RequestUserResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationsController"/> class.
    /// </summary>
    /// <param name="presenter">The presenter.</param>
    /// <param name="resolver">The request user resolver.</param>
    public RecommendationsController(RecommendationPresenter presenter, RequestUserResolver resolver)
    {
        _presenter = presenter;
        _resolver = resolver;
    }

    /// <summary>
    /// Gets personal recommendations with explanations.
    /// </summary>
    /// <param name="limit">Number of items (1-200, default 20).</param>
    /// <param name="type">Optional type: Movie or Series.</param>
    /// <param name="startIndex">Index of the first item.</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The recommendations.</returns>
    [HttpGet("Recommendations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecommendationsResponse>> GetRecommendations([FromQuery] int limit = 20, [FromQuery] string? type = null, [FromQuery] int startIndex = 0, [FromQuery] Guid? userId = null)
    {
        if (type is not null && !string.Equals(type, "Movie", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "Series", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "type must be Movie or Series.");
        }

        var (user, error) = await _resolver.ResolveAsync(this, userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        return _presenter.Recommendations(user, limit, Math.Max(0, startIndex), type);
    }

    /// <summary>
    /// Gets the items similar to an open item, ranked with the user's taste ("Similar, picked for you").
    /// Items rated negatively never appear.
    /// </summary>
    /// <param name="itemId">The open item.</param>
    /// <param name="limit">Number of items (default 12).</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The items.</returns>
    [HttpGet("Similar/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecommendationsResponse>> GetSimilar([FromRoute] Guid itemId, [FromQuery] int limit = 12, [FromQuery] Guid? userId = null)
    {
        var (user, error) = await _resolver.ResolveAsync(this, userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        var response = _presenter.Similar(user, itemId, limit);
        return response is null ? Problem(statusCode: StatusCodes.Status404NotFound, detail: "Item not found.") : response;
    }

    /// <summary>
    /// Gets the "Rate these" items: watched but not rated yet, most recently watched first; empty when there is nothing to rate.
    /// </summary>
    /// <param name="limit">Number of items (default 20).</param>
    /// <param name="type">Optional: "Movie", or "Series" (series and seasons).</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The items.</returns>
    [HttpGet("RateThese")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecommendationsResponse>> GetRateThese([FromQuery] int limit = 20, [FromQuery] string? type = null, [FromQuery] Guid? userId = null)
    {
        if (type is not null && !string.Equals(type, "Movie", StringComparison.OrdinalIgnoreCase) && !string.Equals(type, "Series", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "type must be Movie or Series.");
        }

        var (user, error) = await _resolver.ResolveAsync(this, userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        return _presenter.RateThese(user, limit, type);
    }
}
