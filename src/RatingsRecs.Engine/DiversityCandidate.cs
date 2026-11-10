using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// A candidate for <see cref="Diversifier.Rerank"/>.
/// </summary>
/// <param name="Id">The item id.</param>
/// <param name="Score">The relevance (final score).</param>
/// <param name="Vector">The weighted feature vector.</param>
/// <param name="Collections">The collection keys of the item.</param>
public sealed record DiversityCandidate(Guid Id, double Score, IReadOnlyDictionary<string, double> Vector, IReadOnlyList<string> Collections);
