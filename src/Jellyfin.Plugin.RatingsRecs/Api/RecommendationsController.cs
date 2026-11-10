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
    /// Gets the "Rate these" items (cold start); <c>Hidden</c> is true once the user has enough ratings.
    /// </summary>
    /// <param name="limit">Number of items (default 20).</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The items.</returns>
    [HttpGet("Onboarding")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationsResponse>> GetOnboarding([FromQuery] int limit = 20, [FromQuery] Guid? userId = null)
    {
        var (user, error) = await _resolver.ResolveAsync(this, userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        return _presenter.Onboarding(user, limit);
    }
}
