using System;

namespace RatingsRecs.Engine;

/// <summary>
/// A season as read from the library, before feature aggregation.
/// </summary>
/// <param name="Id">The season id.</param>
/// <param name="IndexNumber">The season number.</param>
/// <param name="Year">The production year of the season, if known.</param>
/// <param name="Played">Whether every episode was watched.</param>
public sealed record SeasonInfo(Guid Id, int? IndexNumber, int? Year, bool Played);
