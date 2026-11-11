using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.RatingsRecs.Recommendations;
using RatingsRecs.Engine;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class RateTheseListTests
{
    private static readonly DateTime Day = new(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc);

    private static readonly Guid MovieWatched = Guid.NewGuid();
    private static readonly Guid MovieWatchedEarlier = Guid.NewGuid();
    private static readonly Guid MovieRated = Guid.NewGuid();
    private static readonly Guid MovieNotWatched = Guid.NewGuid();
    private static readonly Guid MovieHalfWatched = Guid.NewGuid();
    private static readonly Guid MovieNoDateB = Guid.NewGuid();
    private static readonly Guid MovieNoDateA = Guid.NewGuid();
    private static readonly Guid SeriesStarted = Guid.NewGuid();
    private static readonly Guid SeriesNotStarted = Guid.NewGuid();
    private static readonly Guid SeasonFinished = Guid.NewGuid();
    private static readonly Guid SeasonHalf = Guid.NewGuid();

    private static readonly IReadOnlyList<MediaItem> Items =
    [
        Item(MovieWatched, "Movie", "Watched"),
        Item(MovieWatchedEarlier, "Movie", "Watched earlier"),
        Item(MovieRated, "Movie", "Rated"),
        Item(MovieNotWatched, "Movie", "Not watched"),
        Item(MovieHalfWatched, "Movie", "Half watched"),
        Item(MovieNoDateB, "Movie", "B no date"),
        Item(MovieNoDateA, "Movie", "a no date"),
        Item(SeriesStarted, "Series", "Started show"),
        Item(SeriesNotStarted, "Series", "Other show"),
        Item(SeasonFinished, "Season", "Started show - Season 1"),
        Item(SeasonHalf, "Season", "Started show - Season 2"),
    ];

    // What the library snapshot reports: played movies, resumable movies, series with watched episodes
    // (Played = every episode watched) and, in per-season modes, seasons with watched episodes.
    private static readonly IReadOnlyList<PlaybackFact> Facts =
    [
        new(MovieWatched, true, 1, Day.AddDays(3)),
        new(MovieWatchedEarlier, true, 1, Day),
        new(MovieRated, true, 1, Day.AddDays(5)),
        new(MovieHalfWatched, false, 0.4, Day.AddDays(6)),
        new(MovieNoDateB, true, 1, null),
        new(MovieNoDateA, true, 1, null),
        new(SeriesStarted, false, 0.5, Day.AddDays(4)),
        new(SeasonFinished, true, 1, Day.AddDays(2)),
        new(SeasonHalf, false, 0.3, Day.AddDays(4)),
    ];

    private static readonly HashSet<Guid> Rated = [MovieRated];

    private static MediaItem Item(Guid id, string kind, string name) => new() { Id = id, Kind = kind, Name = name };

    [Fact]
    public void WholeSeries_WatchedUnratedMoviesAndStartedSeries_MostRecentFirst_UndatedLastByTitle()
    {
        var list = RateTheseList.Build(Facts, Items, SeriesRatingMode.Series, Rated);

        Assert.Equal(new[] { SeriesStarted, MovieWatched, MovieWatchedEarlier, MovieNoDateA, MovieNoDateB }, list.ToArray());
    }

    [Fact]
    public void PerSeason_OnlyFinishedSeasons_NoSeries()
    {
        var list = RateTheseList.Build(Facts, Items, SeriesRatingMode.Season, Rated);

        Assert.Contains(SeasonFinished, list);
        Assert.DoesNotContain(SeasonHalf, list);
        Assert.DoesNotContain(SeriesStarted, list);
        Assert.Equal(new[] { MovieWatched, SeasonFinished, MovieWatchedEarlier }, list.Take(3).ToArray());
    }

    [Fact]
    public void Both_SeriesAndFinishedSeasons()
    {
        var list = RateTheseList.Build(Facts, Items, SeriesRatingMode.Both, Rated);

        Assert.Equal(new[] { SeriesStarted, MovieWatched, SeasonFinished, MovieWatchedEarlier, MovieNoDateA, MovieNoDateB }, list.ToArray());
    }

    [Fact]
    public void RatedNotWatchedOrPartlyWatched_NeverAppear()
    {
        foreach (var mode in Enum.GetValues<SeriesRatingMode>())
        {
            var list = RateTheseList.Build(Facts, Items, mode, Rated);

            Assert.DoesNotContain(MovieRated, list);
            Assert.DoesNotContain(MovieNotWatched, list);
            Assert.DoesNotContain(MovieHalfWatched, list);
            Assert.DoesNotContain(SeriesNotStarted, list);
        }
    }

    [Fact]
    public void RatingASeriesOrSeason_RemovesItFromTheList()
    {
        var list = RateTheseList.Build(Facts, Items, SeriesRatingMode.Both, new HashSet<Guid> { SeriesStarted, SeasonFinished });

        Assert.DoesNotContain(SeriesStarted, list);
        Assert.DoesNotContain(SeasonFinished, list);
        Assert.Contains(MovieRated, list);
    }

    [Fact]
    public void NothingWatched_GivesAnEmptyList()
    {
        Assert.Empty(RateTheseList.Build([], Items, SeriesRatingMode.Both, new HashSet<Guid>()));
    }
}
