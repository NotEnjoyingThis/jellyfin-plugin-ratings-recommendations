using System;
using System.Globalization;
using System.Text;

namespace RatingsRecs.Engine;

/// <summary>
/// The daily exploration term ε (section 5.3): a number in [0, 1) that depends only on user, UTC date and item,
/// so the list is stable during the day and renews the next day. Uses FNV-1a + SplitMix64 (no process-random hashing).
/// </summary>
public static class StableRandom
{
    /// <summary>
    /// Gets ε for a user, day and item.
    /// </summary>
    /// <param name="userKey">The user key.</param>
    /// <param name="nowUtc">Any time of the day (UTC).</param>
    /// <param name="itemId">The item.</param>
    /// <returns>A value in [0, 1).</returns>
    public static double Unit(string userKey, DateTime nowUtc, Guid itemId)
    {
        var text = (userKey ?? string.Empty) + "|" + nowUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + itemId.ToString("N", CultureInfo.InvariantCulture);
        ulong hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }

        hash += 0x9E3779B97F4A7C15UL;
        hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
        hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
        hash ^= hash >> 31;
        return (hash >> 11) * (1.0 / (1UL << 53));
    }
}
