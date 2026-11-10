using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// A rating or implicit signal of the user on an item.
/// </summary>
public sealed record UserRating
{
    /// <summary>Gets the item id.</summary>
    public Guid ItemId { get; init; }

    /// <summary>Gets the value used by the profile (configured level value or implicit weight).</summary>
    public double Value { get; init; }

    /// <summary>Gets the time of the rating (UTC), for the decay.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>Gets a value indicating whether this is an explicit rating.</summary>
    public bool IsExplicit { get; init; }

    /// <summary>Gets a value indicating whether this is an explicit "Hate it" (-3) rating.</summary>
    public bool IsHate { get; init; }

    /// <summary>Gets a value indicating whether this is an explicit positive (Good/Amazing) rating.</summary>
    public bool IsLoved { get; init; }
}
