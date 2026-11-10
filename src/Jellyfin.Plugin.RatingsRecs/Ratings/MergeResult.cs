namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Outcome of <see cref="IRatingStore.Merge"/>.
/// </summary>
/// <param name="Added">New ratings.</param>
/// <param name="Updated">Existing ratings replaced by a more recent one.</param>
/// <param name="Skipped">Entries ignored (older, invalid, or implicit over explicit).</param>
public sealed record MergeResult(int Added, int Updated, int Skipped);
