namespace RatingsRecs.Engine;

/// <summary>
/// How TV series are rated (docs/DESIGN.md section 4.1).
/// </summary>
public enum SeriesRatingMode
{
    /// <summary>Whole series (default): one rating per series; seasons are ignored.</summary>
    Series,

    /// <summary>Per season: seasons are rated; series ratings are not used.</summary>
    Season,

    /// <summary>Both: a season's own rating if any, otherwise its series' rating.</summary>
    Both,
}
