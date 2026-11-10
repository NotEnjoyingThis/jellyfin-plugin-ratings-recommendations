using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Outcome of the Favourites import.
/// </summary>
/// <param name="Imported">Favourites converted into +3 ratings.</param>
/// <param name="AlreadyRated">Favourites skipped because they already had an explicit rating.</param>
public sealed record FavoritesImportResult(
    [property: JsonPropertyName("imported")] int Imported,
    [property: JsonPropertyName("alreadyRated")] int AlreadyRated);
