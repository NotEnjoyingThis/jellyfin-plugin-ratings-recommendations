using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using RatingsRecs.Engine;

namespace Jellyfin.Plugin.RatingsRecs.Recommendations;

/// <summary>
/// Turns stored ratings and playback facts into engine signals (docs/DESIGN.md section 4, implicit signals):
/// explicit rating = configured level value; Favourite from another client = implicit +3;
/// watched without rating = +0.5; abandoned (progress below 20%, idle 30+ days) = -0.5.
/// An explicit rating always wins over an implicit signal.
/// </summary>
public static class SignalBuilder
{
    /// <summary>
    /// Progress below which a started item counts as abandoned.
    /// </summary>
    public const double AbandonedProgress = 0.2;

    /// <summary>
    /// Idle days after which a started item counts as abandoned.
    /// </summary>
    public const int AbandonedIdleDays = 30;

    /// <summary>
    /// Builds the signals.
    /// </summary>
    /// <param name="stored">The ratings in the store.</param>
    /// <param name="facts">The playback facts.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>One signal per item.</returns>
    public static IReadOnlyList<UserRating> Build(IEnumerable<RatingEntry> stored, IEnumerable<PlaybackFact> facts, PluginConfiguration config, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(config);
        var signals = new Dictionary<Guid, UserRating>();

        foreach (var entry in stored)
        {
            if (entry.IsExplicit)
            {
                signals[entry.ItemId] = new UserRating
                {
                    ItemId = entry.ItemId,
                    Value = EngineSettingsFactory.LevelValue(config, entry.Value),
                    TimestampUtc = entry.UpdatedUtc,
                    IsExplicit = true,
                    IsHate = entry.Value == RatingLevels.Hate,
                    IsLoved = entry.Value > 0,
                };
            }
            else if (config.FavoriteFromOtherClientsCounts)
            {
                signals[entry.ItemId] = new UserRating
                {
                    ItemId = entry.ItemId,
                    Value = config.ImplicitFavoriteValue,
                    TimestampUtc = entry.UpdatedUtc,
                };
            }
        }

        foreach (var fact in facts)
        {
            if (signals.ContainsKey(fact.ItemId))
            {
                continue;
            }

            var when = fact.LastPlayedUtc ?? nowUtc;
            if (fact.Played)
            {
                signals[fact.ItemId] = new UserRating { ItemId = fact.ItemId, Value = config.ImplicitWatchedValue, TimestampUtc = when };
            }
            else if (fact.Progress > 0 && fact.Progress < AbandonedProgress && fact.LastPlayedUtc is DateTime last && (nowUtc - last).TotalDays >= AbandonedIdleDays)
            {
                signals[fact.ItemId] = new UserRating { ItemId = fact.ItemId, Value = config.ImplicitAbandonedValue, TimestampUtc = last };
            }
        }

        return signals.Values.Where(s => Math.Abs(s.Value) > 1e-12 || s.IsExplicit).OrderBy(s => s.ItemId).ToList();
    }
}
