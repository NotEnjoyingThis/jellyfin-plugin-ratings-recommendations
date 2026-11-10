using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// A library item as seen by the engine. Lists are already in cast/credit order; the engine applies the limits.
/// </summary>
public sealed record MediaItem
{
    /// <summary>Gets the item id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the kind ("Movie", "Series", "Season").</summary>
    public string Kind { get; init; } = "Movie";

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the genres.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Gets the directors.</summary>
    public IReadOnlyList<string> Directors { get; init; } = [];

    /// <summary>Gets the actors in cast order.</summary>
    public IReadOnlyList<string> Actors { get; init; } = [];

    /// <summary>Gets the writers in credit order.</summary>
    public IReadOnlyList<string> Writers { get; init; } = [];

    /// <summary>Gets the tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Gets the studios in order.</summary>
    public IReadOnlyList<string> Studios { get; init; } = [];

    /// <summary>Gets the collections (box sets) containing the item.</summary>
    public IReadOnlyList<string> Collections { get; init; } = [];

    /// <summary>Gets the guest stars, most frequent first (seasons only).</summary>
    public IReadOnlyList<string> GuestStars { get; init; } = [];

    /// <summary>Gets the production year.</summary>
    public int? Year { get; init; }

    /// <summary>Gets the community rating (0-10).</summary>
    public double? CommunityRating { get; init; }

    /// <summary>Gets a value indicating whether the user has watched it (for series: any episode watched).</summary>
    public bool Played { get; init; }

    /// <summary>Gets the series id of a season.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the series name of a season.</summary>
    public string? ParentName { get; init; }

    /// <summary>Gets the season number.</summary>
    public int? IndexNumber { get; init; }

    /// <summary>Gets a value indicating whether a series is an anthology (each season stands alone).</summary>
    public bool IsAnthology { get; init; }
}
