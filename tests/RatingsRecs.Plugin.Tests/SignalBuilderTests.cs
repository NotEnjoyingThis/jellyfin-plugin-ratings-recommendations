using System;
using System.Linq;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class SignalBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static RatingEntry Entry(Guid id, int value, string source = RatingSources.Explicit) => new()
    {
        ItemId = id,
        ItemType = "Movie",
        Value = value,
        Source = source,
        CreatedUtc = Now.AddDays(-2),
        UpdatedUtc = Now.AddDays(-1),
    };

    [Fact]
    public void ExplicitRatings_UseTheConfiguredLevelValues()
    {
        var config = new PluginConfiguration { ValueHate = -4, ValueAmazing = 2.5 };
        var hate = Guid.NewGuid();
        var amazing = Guid.NewGuid();

        var signals = SignalBuilder.Build([Entry(hate, -3), Entry(amazing, 3)], [], config, Now);

        var h = signals.Single(s => s.ItemId == hate);
        Assert.Equal(-4, h.Value);
        Assert.True(h.IsExplicit && h.IsHate && !h.IsLoved);
        var a = signals.Single(s => s.ItemId == amazing);
        Assert.Equal(2.5, a.Value);
        Assert.True(a.IsLoved);
        Assert.Equal(Now.AddDays(-1), a.TimestampUtc);
    }

    [Fact]
    public void ImplicitFavourite_UsesItsWeight_AndCanBeTurnedOff()
    {
        var id = Guid.NewGuid();
        var stored = new[] { Entry(id, 3, RatingSources.Implicit) };

        var on = SignalBuilder.Build(stored, [], new PluginConfiguration { ImplicitFavoriteValue = 3 }, Now).Single();
        var off = SignalBuilder.Build(stored, [], new PluginConfiguration { FavoriteFromOtherClientsCounts = false }, Now);

        Assert.Equal(3, on.Value);
        Assert.False(on.IsExplicit);
        Assert.Empty(off);
    }

    [Fact]
    public void WatchedWithoutRating_IsPlusHalf_AbandonedIsMinusHalf()
    {
        var watched = Guid.NewGuid();
        var abandoned = Guid.NewGuid();
        var recentlyStarted = Guid.NewGuid();
        var farAlong = Guid.NewGuid();
        var facts = new[]
        {
            new PlaybackFact(watched, true, 1, Now.AddDays(-3)),
            new PlaybackFact(abandoned, false, 0.1, Now.AddDays(-31)),
            new PlaybackFact(recentlyStarted, false, 0.1, Now.AddDays(-5)),
            new PlaybackFact(farAlong, false, 0.5, Now.AddDays(-60)),
        };

        var signals = SignalBuilder.Build([], facts, new PluginConfiguration(), Now);

        Assert.Equal(0.5, signals.Single(s => s.ItemId == watched).Value);
        Assert.Equal(Now.AddDays(-3), signals.Single(s => s.ItemId == watched).TimestampUtc);
        Assert.Equal(-0.5, signals.Single(s => s.ItemId == abandoned).Value);
        Assert.DoesNotContain(signals, s => s.ItemId == recentlyStarted);
        Assert.DoesNotContain(signals, s => s.ItemId == farAlong);
    }

    [Fact]
    public void ExplicitRating_WinsOverPlaybackSignals()
    {
        var id = Guid.NewGuid();

        var signal = SignalBuilder.Build([Entry(id, -1)], [new PlaybackFact(id, true, 1, Now)], new PluginConfiguration(), Now).Single();

        Assert.Equal(-1, signal.Value);
        Assert.True(signal.IsExplicit);
    }
}
