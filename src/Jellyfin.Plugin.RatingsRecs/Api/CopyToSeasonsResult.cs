using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RatingsRecs.Api;

/// <summary>
/// Result of the copy of a series rating onto its seasons.
/// </summary>
/// <param name="Copied">How many seasons received the rating.</param>
public sealed record CopyToSeasonsResult([property: JsonPropertyName("copied")] int Copied);
