using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Ratings API (docs/DESIGN.md, section 6.5). Every call requires authentication; the target user is the
/// authenticated one. Only administrators (or API keys) may pass <c>userId</c> to act on another user.
/// </summary>
[ApiController]
[Authorize]
[Route("RatingsRecs")]
[Produces(MediaTypeNames.Application.Json)]
public class RatingsController : ControllerBase
{
    private const int MaxBulkIds = 500;
    private const long MaxCsvBytes = 10 * 1024 * 1024;

    private readonly RatingsService _ratings;
    private readonly RequestUserResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingsController"/> class.
    /// </summary>
    /// <param name="ratings">The ratings service.</param>
    /// <param name="resolver">The request user resolver.</param>
    public RatingsController(RatingsService ratings, RequestUserResolver resolver)
    {
        _ratings = ratings;
        _resolver = resolver;
    }

    /// <summary>
    /// Gets the current rating of an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The rating; <c>value</c> is null when not rated.</returns>
    [HttpGet("Ratings/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RatingDto>> GetRating([FromRoute] Guid itemId, [FromQuery] Guid? userId)
    {
        var (user, error) = await ResolveUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        return RatingDto.From(itemId, _ratings.Get(user.Id, itemId));
    }

    /// <summary>
    /// Gets the ratings of several items (for cards). Only rated items are returned.
    /// </summary>
    /// <param name="itemIds">Comma-separated item ids (max 500).</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The ratings.</returns>
    [HttpGet("Ratings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RatingDto>>> GetRatings([FromQuery] string? itemIds, [FromQuery] Guid? userId)
    {
        var (user, error) = await ResolveUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        var ids = new List<Guid>();
        foreach (var part in (itemIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid item id: " + part);
            }

            ids.Add(id);
        }

        if (ids.Count > MaxBulkIds)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "At most " + MaxBulkIds.ToString(CultureInfo.InvariantCulture) + " item ids per request.");
        }

        return _ratings.GetMany(user.Id, ids).Values.Select(e => RatingDto.From(e.ItemId, e)).ToList();
    }

    /// <summary>
    /// Sets or changes a rating: body <c>{ "value": -3 }</c> with -3, -1, 1 or 3.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="request">The rating.</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>The stored rating.</returns>
    [HttpPut("Ratings/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingDto>> SetRating([FromRoute] Guid itemId, [FromBody] SetRatingRequest request, [FromQuery] Guid? userId)
    {
        var (user, error) = await ResolveUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        var outcome = _ratings.Set(user, itemId, request?.Value ?? 0, out var entry);
        return outcome switch
        {
            RatingOutcome.Ok => RatingDto.From(itemId, entry),
            RatingOutcome.InvalidValue => Problem(statusCode: StatusCodes.Status400BadRequest, detail: "value must be -3, -1, 1 or 3."),
            RatingOutcome.NotRateable => Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Only movies and series can be rated."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, detail: "Item not found."),
        };
    }

    /// <summary>
    /// Removes a rating.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>No content.</returns>
    [HttpDelete("Ratings/{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteRating([FromRoute] Guid itemId, [FromQuery] Guid? userId)
    {
        var (user, error) = await ResolveUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        _ratings.Remove(user, itemId);
        return NoContent();
    }

    /// <summary>
    /// Exports the ratings as CSV.
    /// </summary>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>A CSV file.</returns>
    [HttpGet("Export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Export([FromQuery] Guid? userId)
    {
        var (user, error) = await ResolveUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        var csv = _ratings.ExportCsv(user.Id);
        var fileName = string.Format(CultureInfo.InvariantCulture, "ratings-{0}-{1:yyyyMMdd}.csv", SafeFileName(user.Username), DateTime.UtcNow);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// Imports ratings from a CSV body (as produced by Export). The most recent rating wins.
    /// </summary>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>Counts and problems.</returns>
    [HttpPost("Import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<CsvImportResult>> Import([FromQuery] Guid? userId)
    {
        var (user, error) = await ResolveUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        if (Request.ContentLength > MaxCsvBytes)
        {
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "CSV larger than 10 MB.");
        }

        string csv;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            var buffer = new char[MaxCsvBytes + 1];
            var read = await reader.ReadBlockAsync(buffer.AsMemory(), HttpContext.RequestAborted).ConfigureAwait(false);
            if (read > MaxCsvBytes)
            {
                return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "CSV larger than 10 MB.");
            }

            csv = new string(buffer, 0, read);
        }

        if (string.IsNullOrWhiteSpace(csv))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Empty body: send the CSV file content.");
        }

        return _ratings.ImportCsv(user, csv);
    }

    /// <summary>
    /// Converts the user's Favourite movies and series into +3 ratings (explicit ratings are kept).
    /// </summary>
    /// <param name="userId">Optional target user (administrators only).</param>
    /// <returns>Counts.</returns>
    [HttpPost("ImportFavorites")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FavoritesImportResult>> ImportFavorites([FromQuery] Guid? userId)
    {
        var (user, error) = await ResolveUserAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return error!;
        }

        return _ratings.ImportFavorites(user);
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
    }

    private Task<(User? User, ActionResult? Error)> ResolveUserAsync(Guid? requestedUserId) => _resolver.ResolveAsync(this, requestedUserId);
}
