using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Per-user preferences (series rating mode, docs/DESIGN.md section 4.1) and the "copy the series rating
/// onto the seasons" action. Same rules as every endpoint: the user comes from the token, admins may pass userId.
/// </summary>
[ApiController]
[Authorize]
[Route("RatingsRecs")]
[Produces(MediaTypeNames.Application.Json)]
public class PreferencesController : ControllerBase
{
    private readonly IRatingStore _store;
    private readonly RatingsService _ratings;
    private readonly IPluginConfigProvider _config;
    private readonly RequestUserResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreferencesController"/> class.
    /// </summary>
    /// <param name="store">The rating store.</param>
    /// <param name="ratings">The ratings service.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="resolver">The request user resolver.</param>
    public PreferencesController(IRatingStore store, RatingsService ratings, IPluginConfigProvider config, RequestUserResolver resolver)
    {
        _store = store;
        _ratings = ratings;
        _config = config;
        _resolver = resolver;
    }

    /// <summary>
    /// Gets the user's preferences.
    /// </summary>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The preferences.</returns>
    [HttpGet("Preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferencesDto>> GetPreferences([FromQuery] Guid? userId)
    {
        var (user, error) = await _resolver.ResolveAsync(this, userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        return Describe(user.Id);
    }

    /// <summary>
    /// Sets the user's own series rating mode (<c>null</c> = server default). Ratings are never deleted.
    /// </summary>
    /// <param name="request">The preferences.</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The new preferences.</returns>
    [HttpPut("Preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PreferencesDto>> SetPreferences([FromBody] SetPreferencesRequest request, [FromQuery] Guid? userId)
    {
        var (user, error) = await _resolver.ResolveAsync(this, userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        var requested = request?.SeriesMode;
        var mode = SeriesModes.Parse(requested);
        if (!string.IsNullOrEmpty(requested) && mode is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "seriesMode must be Series, Season, Both or null.");
        }

        if (!_config.Current.UsersCanChooseSeriesMode && mode is not null)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "The administrator does not allow users to choose the series rating mode.");
        }

        _store.SetPreferences(user.Id, _store.GetPreferences(user.Id) with { SeriesMode = mode?.ToString() });
        return Describe(user.Id);
    }

    /// <summary>
    /// Copies the series rating onto its seasons that are not rated yet (per-season modes only).
    /// </summary>
    /// <param name="seriesId">The series id.</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>How many seasons were rated.</returns>
    [HttpPost("Ratings/{seriesId}/CopyToSeasons")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CopyToSeasonsResult>> CopyToSeasons([FromRoute] Guid seriesId, [FromQuery] Guid? userId)
    {
        var (user, error) = await _resolver.ResolveAsync(this, userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        return _ratings.CopySeriesRatingToSeasons(user, seriesId, out var copied) switch
        {
            RatingOutcome.Ok => new CopyToSeasonsResult(copied),
            RatingOutcome.NotRateable => Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Seasons can be rated only in the per-season or both modes."),
            RatingOutcome.InvalidValue => Problem(statusCode: StatusCodes.Status400BadRequest, detail: "The series has no rating to copy."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, detail: "Series not found."),
        };
    }

    private PreferencesDto Describe(Guid userId)
    {
        var config = _config.Current;
        var prefs = _store.GetPreferences(userId);
        return new PreferencesDto
        {
            SeriesMode = SeriesModes.Effective(config, prefs).ToString(),
            UserSeriesMode = config.UsersCanChooseSeriesMode ? SeriesModes.Parse(prefs.SeriesMode)?.ToString() : null,
            DefaultSeriesMode = SeriesModes.Default(config).ToString(),
            CanChooseSeriesMode = config.UsersCanChooseSeriesMode,
        };
    }
}
