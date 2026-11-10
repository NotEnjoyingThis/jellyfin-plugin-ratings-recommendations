using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Compat;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Status of the plugin hooks for the settings page (administrators only).
/// </summary>
[ApiController]
[Authorize]
[Route("RatingsRecs")]
public class StatusController : ControllerBase
{
    private readonly HookStatusRegistry _hooks;
    private readonly IAuthorizationContext _authContext;
    private readonly IUserGateway _users;
    private readonly Recommendations.RecommendationService _recommendations;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusController"/> class.
    /// </summary>
    /// <param name="hooks">The hook registry.</param>
    /// <param name="authContext">The authorization context.</param>
    /// <param name="users">The user gateway.</param>
    /// <param name="recommendations">The recommendation service.</param>
    public StatusController(HookStatusRegistry hooks, IAuthorizationContext authContext, IUserGateway users, Recommendations.RecommendationService recommendations)
    {
        _recommendations = recommendations;
        _hooks = hooks;
        _authContext = authContext;
        _users = users;
    }

    /// <summary>
    /// Gets the build and the hook statuses.
    /// </summary>
    /// <returns>The status.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PluginStatusDto>> GetStatus()
    {
        var auth = await _authContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);
        if (!auth.IsApiKey && (auth.User is null || !_users.IsAdministrator(auth.User)))
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Administrators only.");
        }

        return new PluginStatusDto
        {
            Version = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0",
            TargetAbi = BuildInfo.TargetAbi,
            JellyfinLine = BuildInfo.JellyfinLine,
            Hooks = _hooks.GetAll(),
            SeasonCredits = _recommendations.CreditsReport,
        };
    }
}
