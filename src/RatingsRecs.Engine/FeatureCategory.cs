using System;
using System.Collections.Generic;

namespace RatingsRecs.Engine;

/// <summary>
/// Feature categories (docs/DESIGN.md, section 5.1; guest stars are used by the per-season mode, section 5.7).
/// </summary>
public enum FeatureCategory
{
    /// <summary>Genres.</summary>
    Genre,

    /// <summary>Directors.</summary>
    Director,

    /// <summary>Main cast.</summary>
    Actor,

    /// <summary>Writers.</summary>
    Writer,

    /// <summary>Tags / keywords.</summary>
    Tag,

    /// <summary>Studios / TV networks.</summary>
    Studio,

    /// <summary>Decade of production.</summary>
    Decade,

    /// <summary>Collection / saga (box set).</summary>
    Collection,

    /// <summary>Guest stars (seasons only).</summary>
    GuestStar,
}
