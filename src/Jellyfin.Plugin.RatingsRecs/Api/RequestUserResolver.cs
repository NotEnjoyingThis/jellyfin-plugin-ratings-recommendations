using System;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.RatingsRecs.Compat;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Resolves the user an API call acts on: always the authenticated user, unless an administrator
/// (or an API key) passes <c>userId</c>.
/// </summary>
public sealed class RequestUserResolver
{
    private readonly IAuthorizationContext _authContext;
    private readonly IUserGateway _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestUserResolver"/> class.
    /// </summary>
    /// <param name="authContext">The authorization context.</param>
    /// <param name="users">The user gateway.</param>
    public RequestUserResolver(IAuthorizationContext authContext, IUserGateway users)
    {
        _authContext = authContext;
        _users = users;
    }

    /// <summary>
    /// Resolves the target user.
    /// </summary>
    /// <param name="controller">The calling controller.</param>
    /// <param name="requestedUserId">The optional <c>userId</c> parameter.</param>
    /// <returns>The user, or an error result.</returns>
    public async Task<(User? User, ActionResult? Error)> ResolveAsync(ControllerBase controller, Guid? requestedUserId)
    {
        ArgumentNullException.ThrowIfNull(controller);
        var auth = await _authContext.GetAuthorizationInfo(controller.HttpContext).ConfigureAwait(false);
        var caller = auth.User;

        if (requestedUserId is null || requestedUserId == Guid.Empty || (caller is not null && requestedUserId == caller.Id))
        {
            return caller is null
                ? (null, controller.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "userId is required when authenticating with an API key."))
                : (caller, null);
        }

        var isAdmin = auth.IsApiKey || (caller is not null && _users.IsAdministrator(caller));
        if (!isAdmin)
        {
            return (null, controller.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Only administrators can act on other users."));
        }

        var target = _users.GetUser(requestedUserId.Value);
        return target is null
            ? (null, controller.Problem(statusCode: StatusCodes.Status404NotFound, detail: "User not found."))
            : (target, null);
    }
}
