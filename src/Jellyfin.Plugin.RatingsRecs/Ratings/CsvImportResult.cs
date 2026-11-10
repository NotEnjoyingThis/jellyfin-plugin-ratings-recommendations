using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Outcome of a CSV import.
/// </summary>
/// <param name="Added">New ratings.</param>
/// <param name="Updated">Ratings replaced by more recent ones.</param>
/// <param name="Skipped">Rows ignored because the existing rating is more recent.</param>
/// <param name="Unmatched">Rows whose item was not found in the library.</param>
/// <param name="UnmatchedNames">The first unmatched names.</param>
/// <param name="Errors">The first invalid lines.</param>
public sealed record CsvImportResult(
    [property: JsonPropertyName("added")] int Added,
    [property: JsonPropertyName("updated")] int Updated,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("unmatched")] int Unmatched,
    [property: JsonPropertyName("unmatchedNames")] IReadOnlyList<string> UnmatchedNames,
    [property: JsonPropertyName("errors")] IReadOnlyList<CsvError> Errors);
