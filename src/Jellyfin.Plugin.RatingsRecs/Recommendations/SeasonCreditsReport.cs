using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// How many seasons lack per-episode credits (directors, writers, guest stars): with poor metadata the
/// per-season mode behaves almost like the whole-series mode (docs/DESIGN.md section 4.1).
/// </summary>
/// <param name="Seasons">The number of seasons in the library.</param>
/// <param name="WithoutCredits">The seasons without per-episode credits.</param>
public sealed record SeasonCreditsReport(
    [property: JsonPropertyName("seasons")] int Seasons,
    [property: JsonPropertyName("withoutCredits")] int WithoutCredits)
{
    /// <summary>
    /// Gets the percentage of seasons without credits (0 when there are no seasons).
    /// </summary>
    [JsonPropertyName("percentWithoutCredits")]
    public int PercentWithoutCredits => Seasons == 0 ? 0 : (int)Math.Round(100.0 * WithoutCredits / Seasons);
}
