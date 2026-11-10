using System;
using System.Collections.Generic;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// What the engine needs from the library for one user.
/// </summary>
/// <param name="Items">The items the user can access, as engine DTOs.</param>
/// <param name="Kinds">Item kind ("Movie", "Series") by id.</param>
/// <param name="Facts">Playback facts for implicit signals.</param>
public sealed record LibrarySnapshot(
    IReadOnlyList<MediaItem> Items,
    IReadOnlyDictionary<Guid, string> Kinds,
    IReadOnlyList<PlaybackFact> Facts);
