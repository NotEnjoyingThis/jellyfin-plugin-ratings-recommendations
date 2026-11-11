using System;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Home;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using Jellyfin.Plugin.RatingsRecs.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Serves the web client script and stylesheet (embedded resources). Anonymous because a
/// <c>&lt;script src&gt;</c> tag sends no Jellyfin token; the files contain no user data.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("RatingsRecs/Web")]
public class WebController : ControllerBase
{
    private const string ForYouPage = "<div class=\"rr-pluginpages-foryou\"><a href=\"#/ratingsrecs\">For you / Per te</a></div>";

    private readonly IPluginConfigProvider _config;
    private readonly HookStatusRegistry _hooks;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebController"/> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="hooks">The hook registry.</param>
    public WebController(IPluginConfigProvider config, HookStatusRegistry hooks)
    {
        _config = config;
        _hooks = hooks;
    }

    /// <summary>
    /// Gets the web client script.
    /// </summary>
    /// <returns>JavaScript.</returns>
    [HttpGet("ratings.js")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public ActionResult GetScript()
    {
        var config = _config.Current;
        var hssActive = _hooks.Get(HomeSectionsBridge.HomeScreenSectionsHook)?.State == HookState.Active;
        return Serve(WebAssets.BuildScript(config, WebAssets.HomeRows(config.HomeRowsMode, hssActive)), "application/javascript; charset=utf-8");
    }

    /// <summary>
    /// Gets the web client stylesheet.
    /// </summary>
    /// <returns>CSS.</returns>
    [HttpGet("ratings.css")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public ActionResult GetStylesheet() => Serve(WebAssets.ReadText("ratings.css") ?? string.Empty, "text/css; charset=utf-8");

    /// <summary>
    /// Gets the page registered in Plugin Pages; the web script sends it on to the "For you" page (#/ratingsrecs).
    /// </summary>
    /// <returns>HTML.</returns>
    [HttpGet("foryou.html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetForYouPage() => Serve(ForYouPage, "text/html; charset=utf-8");

    private ActionResult Serve(string content, string contentType)
    {
        var etag = WebAssets.ETag(content);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "no-cache";
        if (Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(Encoding.UTF8.GetBytes(content), contentType);
    }
}
