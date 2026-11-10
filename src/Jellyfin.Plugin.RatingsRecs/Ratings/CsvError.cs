using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// An invalid CSV line.
/// </summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Message">What is wrong.</param>
public sealed record CsvError(
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("message")] string Message);
