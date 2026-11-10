namespace RatingsRecs.Engine;

/// <summary>
/// Static information about the recommendation engine.
/// </summary>
public static class EngineInfo
{
    /// <summary>
    /// Gets the version of the scoring model. Bump it whenever formulas change,
    /// so cached recommendations computed with an older model are discarded.
    /// </summary>
    public static int ModelVersion => 1;
}
