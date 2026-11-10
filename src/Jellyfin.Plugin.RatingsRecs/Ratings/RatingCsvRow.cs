namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// One CSV line: the rating plus descriptive columns used to re-match items on another server.
/// </summary>
public sealed record RatingCsvRow
{
    /// <summary>
    /// Gets the rating. <c>ItemId</c> may be empty when only the descriptive columns are known.
    /// </summary>
    public RatingEntry Entry { get; init; } = new();

    /// <summary>
    /// Gets the item name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the production year.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Gets the IMDb id.
    /// </summary>
    public string? ImdbId { get; init; }

    /// <summary>
    /// Gets the TMDb id.
    /// </summary>
    public string? TmdbId { get; init; }

    /// <summary>
    /// Gets the TVDb id.
    /// </summary>
    public string? TvdbId { get; init; }
}
