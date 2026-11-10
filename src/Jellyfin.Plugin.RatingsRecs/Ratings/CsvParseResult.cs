using System.Collections.Generic;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Result of <see cref="RatingsCsv.Parse"/>.
/// </summary>
/// <param name="Rows">Valid rows.</param>
/// <param name="Errors">Invalid lines.</param>
public sealed record CsvParseResult(IReadOnlyList<RatingCsvRow> Rows, IReadOnlyList<CsvError> Errors);
