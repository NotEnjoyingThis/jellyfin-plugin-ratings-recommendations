using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// Section 5.4 rule 3: MMR re-ranking of the top items (λ·relevance − (1−λ)·max similarity to the chosen ones)
/// and at most N items of the same saga in the first window.
/// </summary>
public static class Diversifier
{
    /// <summary>
    /// Re-ranks candidates.
    /// </summary>
    /// <param name="candidates">The candidates (any order).</param>
    /// <param name="lambda">The MMR λ.</param>
    /// <param name="poolSize">How many top items are re-ranked.</param>
    /// <param name="maxPerCollection">The saga cap.</param>
    /// <param name="capWindow">The window of the saga cap.</param>
    /// <returns>The ordered ids.</returns>
    public static IReadOnlyList<Guid> Rerank(IReadOnlyList<DiversityCandidate> candidates, double lambda, int poolSize, int maxPerCollection, int capWindow)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var sorted = candidates.OrderByDescending(c => c.Score).ThenBy(c => c.Id).ToList();
        var pool = sorted.Take(Math.Max(0, poolSize)).ToList();
        var ordered = new List<DiversityCandidate>(sorted.Count);
        var norms = pool.ToDictionary(c => c.Id, c => Norm(c.Vector));

        while (pool.Count > 0)
        {
            DiversityCandidate? best = null;
            var bestValue = double.NegativeInfinity;
            foreach (var candidate in pool)
            {
                var maxSimilarity = 0.0;
                foreach (var chosen in ordered)
                {
                    maxSimilarity = Math.Max(maxSimilarity, Cosine(candidate.Vector, norms[candidate.Id], chosen.Vector, norms[chosen.Id]));
                }

                var value = (lambda * candidate.Score) - ((1 - lambda) * maxSimilarity);
                if (value > bestValue + 1e-12)
                {
                    best = candidate;
                    bestValue = value;
                }
            }

            ordered.Add(best!);
            pool.Remove(best!);
        }

        ordered.AddRange(sorted.Skip(Math.Max(0, poolSize)));
        return ApplyCollectionCap(ordered, maxPerCollection, capWindow);
    }

    /// <summary>
    /// Cosine similarity of two sparse vectors.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The similarity in [0, 1] for non-negative vectors.</returns>
    public static double Cosine(IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Cosine(a, Norm(a), b, Norm(b));
    }

    private static double Cosine(IReadOnlyDictionary<string, double> a, double normA, IReadOnlyDictionary<string, double> b, double normB)
    {
        if (normA <= 0 || normB <= 0)
        {
            return 0;
        }

        var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
        var dot = 0.0;
        foreach (var (key, value) in small)
        {
            if (large.TryGetValue(key, out var other))
            {
                dot += value * other;
            }
        }

        return dot / (normA * normB);
    }

    private static double Norm(IReadOnlyDictionary<string, double> vector) => Math.Sqrt(vector.Values.Sum(v => v * v));

    private static List<Guid> ApplyCollectionCap(List<DiversityCandidate> ordered, int maxPerCollection, int window)
    {
        var result = new List<Guid>(ordered.Count);
        var deferred = new List<DiversityCandidate>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var windowClosed = maxPerCollection <= 0 || window <= 0;

        foreach (var candidate in ordered)
        {
            if (windowClosed)
            {
                result.Add(candidate.Id);
                continue;
            }

            if (candidate.Collections.Any(c => counts.GetValueOrDefault(c) >= maxPerCollection))
            {
                deferred.Add(candidate);
                continue;
            }

            result.Add(candidate.Id);
            foreach (var collection in candidate.Collections)
            {
                counts[collection] = counts.GetValueOrDefault(collection) + 1;
            }

            if (result.Count >= window)
            {
                windowClosed = true;
                result.AddRange(deferred.Select(d => d.Id));
                deferred.Clear();
            }
        }

        result.AddRange(deferred.Select(d => d.Id));
        return result;
    }
}
