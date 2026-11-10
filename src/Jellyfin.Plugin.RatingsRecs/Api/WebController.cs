using System;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.RatingsRecs.Configuration;
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
    private readonly IPluginConfigProvider _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebController"/> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    public WebController(IPluginConfigProvider config)
    {
        _config = config;
    }

    /// <summary>
    /// Gets the web client script.
    /// </summary>
    /// <returns>JavaScript.</returns>
    [HttpGet("ratings.js")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public ActionResult GetScript() => Serve(WebAssets.BuildScript(_config.Current), "application/javascript; charset=utf-8");

    /// <summary>
    /// Gets the web client stylesheet.
    /// </summary>
    /// <returns>CSS.</returns>
    [HttpGet("ratings.css")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public ActionResult GetStylesheet() => Serve(WebAssets.ReadText("ratings.css") ?? string.Empty, "text/css; charset=utf-8");

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
