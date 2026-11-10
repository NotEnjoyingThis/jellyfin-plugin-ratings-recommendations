using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// The "Rate these" row of section 5.5: items to rate to calibrate the profile quickly. Watched-but-unrated
/// items first, then the best-known ones (community rating); one item per main genre before a genre repeats.
/// </summary>
public static class Onboarding
{
    /// <summary>
    /// Selects the items.
    /// </summary>
    /// <param name="request">The request (library and ratings).</param>
    /// <param name="limit">How many items.</param>
    /// <returns>The item ids in display order.</returns>
    public static IReadOnlyList<Guid> Select(RecommendationRequest request, int limit)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rated = request.Ratings.Select(r => r.ItemId).ToHashSet();
        var preferred = request.Library
            .Where(i => !rated.Contains(i.Id))
            .GroupBy(i => i.Id)
            .Select(g => g.First())
            .OrderByDescending(i => i.Played)
            .ThenByDescending(i => i.CommunityRating ?? -1)
            .ThenBy(i => i.Id)
            .ToList();

        var result = new List<Guid>(Math.Min(limit, preferred.Count));
        var perGenre = new Dictionary<string, int>(StringComparer.Ordinal);
        var taken = new HashSet<Guid>();

        // Round-robin passes: in pass p a genre may hold at most p items.
        for (var pass = 1; result.Count < limit && taken.Count < preferred.Count; pass++)
        {
            foreach (var item in preferred)
            {
                if (result.Count >= limit)
                {
                    break;
                }

                if (taken.Contains(item.Id))
                {
                    continue;
                }

                var genre = MainGenre(item);
                if (perGenre.GetValueOrDefault(genre) >= pass)
                {
                    continue;
                }

                perGenre[genre] = perGenre.GetValueOrDefault(genre) + 1;
                taken.Add(item.Id);
                result.Add(item.Id);
            }
        }

        return result;
    }

    private static string MainGenre(MediaItem item)
        => item.Genres.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g))?.Trim().ToLowerInvariant() ?? string.Empty;
}
