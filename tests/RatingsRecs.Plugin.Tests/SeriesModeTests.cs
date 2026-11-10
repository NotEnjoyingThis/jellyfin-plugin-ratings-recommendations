using System;
using System.IO;
using Jellyfin.Plugin.RatingsRecs.Configuration;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using Microsoft.Extensions.Logging.Abstractions;
using RatingsRecs.Engine;
using RatingsRecs.Plugin.Tests.Support;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public sealed class SeriesModeTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly Guid _user = Guid.NewGuid();

    public void Dispose() => _dir.Dispose();

    private RatingStore NewStore() => new(_dir.Path, NullLogger<RatingStore>.Instance, TimeProvider.System);

    [Fact]
    public void DefaultMode_IsWholeSeries()
    {
        Assert.Equal(SeriesRatingMode.Series, SeriesModes.Effective(new PluginConfiguration(), new UserPreferences()));
    }

    [Fact]
    public void UserChoice_WinsOnlyWhenAllowed()
    {
        var prefs = new UserPreferences { SeriesMode = "season" };

        Assert.Equal(SeriesRatingMode.Season, SeriesModes.Effective(new PluginConfiguration(), prefs));
        Assert.Equal(SeriesRatingMode.Both, SeriesModes.Effective(new PluginConfiguration { UsersCanChooseSeriesMode = false, SeriesRatingMode = "Both" }, prefs));
        Assert.Equal(SeriesRatingMode.Series, SeriesModes.Effective(new PluginConfiguration { SeriesRatingMode = "nonsense" }, new UserPreferences { SeriesMode = "nonsense" }));
    }

    [Fact]
    public void ChangingMode_NeverLosesRatings()
    {
        var store = NewStore();
        var series = Guid.NewGuid();
        var season1 = Guid.NewGuid();
        var season2 = Guid.NewGuid();
        store.SetExplicit(_user, series, "Series", RatingLevels.Good);
        store.SetPreferences(_user, new UserPreferences { SeriesMode = "Season" });
        store.SetExplicit(_user, season1, "Season", RatingLevels.Amazing, series);
        store.SetExplicit(_user, season2, "Season", RatingLevels.Hate, series);

        foreach (var mode in new[] { "Series", "Both", "Season", null })
        {
            store.SetPreferences(_user, new UserPreferences { SeriesMode = mode });
        }

        var reloaded = NewStore();
        Assert.Equal(3, reloaded.GetAll(_user).Count);
        Assert.Equal(RatingLevels.Good, reloaded.Get(_user, series)!.Value);
        Assert.Equal(series, reloaded.Get(_user, season1)!.SeriesId);
        Assert.Equal("Season", reloaded.Get(_user, season2)!.ItemType);
        Assert.Null(reloaded.GetPreferences(_user).SeriesMode);
    }

    [Fact]
    public void Preferences_ArePersistedInTheUserFile_AndRaiseRatingsChanged()
    {
        var store = NewStore();
        var changes = 0;
        store.RatingsChanged += (_, _) => changes++;

        store.SetPreferences(_user, new UserPreferences { SeriesMode = "Both" });

        Assert.Equal(1, changes);
        Assert.Equal("Both", NewStore().GetPreferences(_user).SeriesMode);
        Assert.Contains("\"seriesMode\": \"Both\"", File.ReadAllText(Path.Combine(_dir.Path, _user.ToString("D") + ".json")), StringComparison.Ordinal);
    }
}
