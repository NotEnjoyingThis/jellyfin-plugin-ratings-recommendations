using System;
using System.Collections.Generic;
using System.Linq;
using RatingsRecs.Engine;
using Xunit;
using static RatingsRecs.Engine.Tests.EngineTestData;

namespace RatingsRecs.Engine.Tests;

public class SeasonModeTests
{
    private static readonly EngineSettings Settings = new();

    private static MediaItem Series(string name, string[] genres, string[] cast, bool anthology = false, double rating = 7) => new()
    {
        Id = Id(name),
        Kind = "Series",
        Name = name,
        Genres = genres,
        Actors = cast,
        CommunityRating = rating,
        Year = 2015,
        IsAnthology = anthology,
    };

    private static MediaItem Season(MediaItem series, int number, string[]? directors = null, string[]? writers = null, double? rating = null)
    {
        EpisodeCredits[] episodes =
        [
            new EpisodeCredits { Directors = directors ?? [], Writers = writers ?? [], CommunityRating = rating, Year = 2015 + number },
        ];
        return SeasonFeatureBuilder.Build(series, new SeasonInfo(Id(series.Name + " S" + number), number, null, false), episodes, Settings);
    }

    // ---------- Season features (section 5.7) ----------

    [Fact]
    public void SeasonFeatures_AggregateEpisodeCredits_AndTakeTheRestFromTheSeries()
    {
        var series = Series("Precinct", ["Crime", "Drama"], ["A1", "A2", "A3", "A4", "A5", "A6"]) with { Studios = ["Net"], Tags = ["police"] };
        EpisodeCredits[] episodes =
        [
            new() { Directors = ["D1"], Writers = ["W1"], GuestStars = ["G1", "G2"], CommunityRating = 8, Year = 2013 },
            new() { Directors = ["D2"], Writers = ["W1", "W2"], GuestStars = ["G2", "G3", "G4", "G5", "G6", "G7"], CommunityRating = 9, Year = 2014 },
        ];

        var season = SeasonFeatureBuilder.Build(series, new SeasonInfo(Guid.NewGuid(), 2, null, false), episodes, Settings);

        Assert.Equal("Season", season.Kind);
        Assert.Equal(series.Id, season.ParentId);
        Assert.Equal(2, season.IndexNumber);
        Assert.Equal(series.Genres, season.Genres);
        Assert.Equal(series.Tags, season.Tags);
        Assert.Equal(series.Studios, season.Studios);
        Assert.Equal(5, season.Actors.Count);
        Assert.Equal(new[] { "D1", "D2" }, season.Directors);
        Assert.Equal(new[] { "W1", "W2" }, season.Writers);
        Assert.Equal(5, season.GuestStars.Count);
        Assert.Equal("G2", season.GuestStars[0]);
        Assert.Equal(8.5, season.CommunityRating);
        Assert.Equal(2013, season.Year);
    }

    [Fact]
    public void SeasonWithoutCredits_UsesTheSeriesFeatures()
    {
        var series = Series("Old Show", ["Comedy"], ["A"]) with { Directors = ["Showrunner"], Writers = ["Creator"] };
        EpisodeCredits[] episodes = [new() { CommunityRating = null }, new()];

        var season = SeasonFeatureBuilder.Build(series, new SeasonInfo(Guid.NewGuid(), 1, 1999, false), episodes, Settings);

        Assert.False(SeasonFeatureBuilder.HasEpisodeCredits(episodes));
        Assert.Equal(series.Directors, season.Directors);
        Assert.Equal(series.Writers, season.Writers);
        Assert.Empty(season.GuestStars);
        Assert.Equal(series.CommunityRating, season.CommunityRating);
        Assert.Equal(1999, season.Year);
    }

    // ---------- Modes ----------

    private static (List<MediaItem> Library, List<UserRating> Ratings) MixedLibrary()
    {
        var precinct = Series("Precinct", ["Crime"], ["Cop A", "Cop B"]);
        var tales = Series("Tales", ["Sci-Fi"], ["Host"], anthology: true);
        var library = new List<MediaItem>
        {
            Movie("Heist", ["Crime"], ["Dir X"], rating: 7),
            Movie("Space", ["Sci-Fi"], ["Dir Y"], rating: 8),
            Movie("Drama", ["Drama"], ["Dir Z"], rating: 6),
            precinct,
            tales,
            Series("Other", ["Crime"], ["Cop B"]),
            Season(precinct, 1, ["D1"], ["W1"], 8),
            Season(precinct, 2, ["D2"], ["W2"], 7),
            Season(tales, 1, ["D3"], ["W3"], 6),
            Season(tales, 2, ["D4"], ["W1"], 9),
        };
        var ratings = new List<UserRating> { Rate("Heist", 3), Rate("Drama", -1) };
        return (library, ratings);
    }

    [Fact]
    public void WholeSeriesMode_IgnoresSeasons_AndGivesExactlyTheM3Result()
    {
        var (library, ratings) = MixedLibrary();
        var withoutSeasons = library.Where(i => i.Kind != "Season").ToList();
        var seasonRatings = ratings.Concat([Rate("Precinct S1", 3), Rate("Tales S2", -3)]).ToList();

        var m3 = RecommendationEngine.Recommend(new RecommendationRequest { Library = withoutSeasons, Ratings = ratings, UserKey = "u", NowUtc = Now });
        var mode = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = seasonRatings, UserKey = "u", NowUtc = Now, Mode = SeriesRatingMode.Series });

        Assert.Equal(m3.Items.Select(i => i.ItemId), mode.Items.Select(i => i.ItemId));
        Assert.Equal(m3.Items.Select(i => i.Score), mode.Items.Select(i => i.Score));
        Assert.Equal(m3.ExplicitRatingCount, mode.ExplicitRatingCount);
        Assert.Equal(m3.IsColdStart, mode.IsColdStart);
    }

    [Fact]
    public void OppositeVotesOnTwoSeasons_MoveTheProfileOnlyOnWritersAndDirectors()
    {
        var (library, _) = MixedLibrary();
        var settings = new EngineSettings { UseIdf = false, HalfLifeDays = 0 };
        var index = new FeatureIndex(library.Where(i => i.Name != "Precinct").ToList(), settings);
        var ratings = new List<UserRating> { Rate("Precinct S1", 3), Rate("Precinct S2", -3) };

        var profile = TasteProfile.Build(index, ratings, Now, settings);

        // Genres and cast come from the series and cancel out...
        Assert.Equal(0, profile.Weight(FeatureCategory.Genre, "Crime"), 9);
        Assert.Equal(0, profile.Weight(FeatureCategory.Actor, "Cop A"), 9);
        Assert.Equal(0, profile.Weight(FeatureCategory.Actor, "Cop B"), 9);
        // ...what remains are the credits that differ between the seasons.
        Assert.Equal(1.0, profile.Weight(FeatureCategory.Writer, "W1"), 9);
        Assert.Equal(1.0, profile.Weight(FeatureCategory.Director, "D1"), 9);
        Assert.Equal(-1.0, profile.Weight(FeatureCategory.Writer, "W2"), 9);
        Assert.Equal(-1.0, profile.Weight(FeatureCategory.Director, "D2"), 9);
    }

    [Fact]
    public void ASeriesWithOneVeryCloseSeason_RisesInSeasonMode()
    {
        var target = Series("Target", ["Western"], ["Nobody"]);
        var plain = Series("Plain", ["Western"], ["Nobody else"]);
        var liked = Series("Liked show", ["Crime"], ["X"]);
        var library = new List<MediaItem>
        {
            target, plain, liked,
            Season(target, 1, ["Dull"], ["Dull"], 6),
            Season(target, 2, ["Star Director"], ["Star Writer"], 6),
            Season(target, 3, ["Dull 2"], ["Dull 2"], 6),
            Season(plain, 1, ["P1"], ["P1"], 6.5),
            Season(plain, 2, ["P2"], ["P2"], 6.5),
            Season(liked, 1, ["Star Director"], ["Star Writer"], 8),
        };
        for (var i = 0; i < 6; i++)
        {
            library.Add(Movie("Filler " + i, ["Genre" + i], ["F" + i], rating: 6));
        }

        var ratings = new List<UserRating> { Rate("Liked show S1", 3) };
        for (var i = 0; i < 5; i++)
        {
            ratings.Add(Rate("Filler " + i, i % 2 == 0 ? 1 : -1));
        }

        RecommendationResult Run(SeriesRatingMode mode) =>
            RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now, Mode = mode });

        var season = Run(SeriesRatingMode.Season);
        var ids = season.Items.Select(i => i.ItemId).ToList();
        Assert.True(ids.IndexOf(Id("Target")) < ids.IndexOf(Id("Plain")));
        var targetItem = season.Items.Single(i => i.ItemId == Id("Target"));
        Assert.Equal(2, targetItem.Explanation.BestSeasonNumber);
        Assert.Equal(1, targetItem.Explanation.StartSeasonNumber);
        Assert.NotNull(targetItem.Explanation.SimilarSeason);
        Assert.Equal("Liked show", targetItem.Explanation.SimilarSeason!.SeriesName);
        Assert.Equal(1, targetItem.Explanation.SimilarSeason.SeasonNumber);
        Assert.Contains(targetItem.Explanation.SimilarSeason.SharedValue, new[] { "Star Writer", "Star Director" });

        // In whole-series mode the season credits do not exist, so "Target" has no advantage.
        var whole = Run(SeriesRatingMode.Series);
        Assert.True(season.Items.Single(i => i.ItemId == Id("Target")).Taste > whole.Items.Single(i => i.ItemId == Id("Target")).Taste);
    }

    [Fact]
    public void SeriesScore_IsSixTenthsBestPlusFourTenthsMean()
    {
        var (library, ratings) = MixedLibrary();
        var result = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now, Mode = SeriesRatingMode.Season, Settings = new EngineSettings { DiversityLambda = 1 } });

        // Recompute the seasons of "Precinct" as standalone units through a library where they are the only seasons.
        var precinct = result.Items.Single(i => i.ItemId == Id("Precinct"));
        Assert.InRange(precinct.Score, 0, 1);
        Assert.DoesNotContain(result.Items, i => i.ItemId == Id("Precinct S1") || i.ItemId == Id("Precinct S2"));

        var custom = RecommendationEngine.Recommend(new RecommendationRequest
        {
            Library = library,
            Ratings = ratings,
            UserKey = "u",
            NowUtc = Now,
            Mode = SeriesRatingMode.Season,
            Settings = new EngineSettings { SeriesBestSeasonWeight = 1, SeriesAverageSeasonWeight = 0 },
        });
        var meanOnly = RecommendationEngine.Recommend(new RecommendationRequest
        {
            Library = library,
            Ratings = ratings,
            UserKey = "u",
            NowUtc = Now,
            Mode = SeriesRatingMode.Season,
            Settings = new EngineSettings { SeriesBestSeasonWeight = 0, SeriesAverageSeasonWeight = 1 },
        });
        var best = custom.Items.Single(i => i.ItemId == Id("Precinct")).Score;
        var mean = meanOnly.Items.Single(i => i.ItemId == Id("Precinct")).Score;
        Assert.Equal((0.6 * best) + (0.4 * mean), precinct.Score, 9);
        Assert.True(best >= mean);
    }

    [Fact]
    public void Anthology_StartsFromTheBestSeason()
    {
        var (library, _) = MixedLibrary();
        var ratings = new List<UserRating> { Rate("Heist", 3), Rate("Space", 3) };

        var result = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now, Mode = SeriesRatingMode.Season });

        var tales = result.Items.Single(i => i.ItemId == Id("Tales")).Explanation;
        Assert.Equal(tales.BestSeasonNumber, tales.StartSeasonNumber);
        var precinct = result.Items.Single(i => i.ItemId == Id("Precinct")).Explanation;
        Assert.Equal(1, precinct.StartSeasonNumber);
    }

    [Fact]
    public void SeasonMode_UsesSeasonRatings_IgnoresSeriesRatings_AndExcludesRatedSeries()
    {
        var (library, ratings) = MixedLibrary();
        var withRatings = ratings.Concat([Rate("Tales S1", 3), Rate("Other", -3)]).ToList();

        var result = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = withRatings, UserKey = "u", NowUtc = Now, Mode = SeriesRatingMode.Season });

        Assert.DoesNotContain(result.Items, i => i.ItemId == Id("Tales"));
        Assert.DoesNotContain(result.Items, i => i.ItemId == Id("Other"));
        Assert.Contains(result.Items, i => i.ItemId == Id("Precinct"));
        // Heist, Drama, Tales S1 and "Other" (a series without seasons is rated as a whole in every mode).
        Assert.Equal(4, result.ExplicitRatingCount);
    }

    [Fact]
    public void BothMode_UnratedSeasonsInheritTheSeriesRating()
    {
        var (library, _) = MixedLibrary();
        var settings = new EngineSettings { UseIdf = false, HalfLifeDays = 0 };
        var ratings = new List<UserRating> { Rate("Precinct", 3), Rate("Precinct S2", -1) };

        // "Both": S1 takes the series rating (+3), S2 keeps its own (-1).
        var both = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now, Mode = SeriesRatingMode.Both, Settings = settings });
        var season = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now, Mode = SeriesRatingMode.Season, Settings = settings });

        var tales = both.Items.Single(i => i.ItemId == Id("Tales"));
        var talesSeasonOnly = season.Items.Single(i => i.ItemId == Id("Tales"));
        // Tales S2 shares writer W1 with Precinct S1: liked only in "Both" (through the series rating).
        Assert.True(tales.RawTaste > talesSeasonOnly.RawTaste);
        Assert.Equal(2, both.ExplicitRatingCount);
    }
}
