using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RatingsRecs.Engine;
using Xunit;
using static RatingsRecs.Engine.Tests.EngineTestData;

namespace RatingsRecs.Engine.Tests;

public class RecommendationEngineTests
{
    // ---------- Worked example of docs/DESIGN.md section 5.6 ----------

    private static List<MediaItem> ExampleLibrary() =>
    [
        Movie("Alien", ["Science Fiction", "Horror"], ["Ridley Scott"]),
        Movie("Prometheus", ["Science Fiction", "Horror"], ["Ridley Scott"]),
        Movie("Mamma Mia!", ["Musical", "Comedy"]),
        Movie("Blade Runner", ["Science Fiction", "Thriller"], ["Ridley Scott"]),
        Movie("Another Musical", ["Musical", "Comedy"]),
    ];

    private static RecommendationRequest ExampleRequest() => new()
    {
        Library = ExampleLibrary(),
        Ratings = [Rate("Alien", 3), Rate("Prometheus", -1), Rate("Mamma Mia!", -3)],
        UserKey = "user",
        NowUtc = Now,
        Settings = ExampleSettings(),
    };

    [Fact]
    public void Example56_Profile()
    {
        var request = ExampleRequest();
        var index = new FeatureIndex(request.Library, request.Settings);
        var profile = TasteProfile.Build(index, request.Ratings, request.NowUtc, request.Settings);

        Assert.Equal(0.50, profile.Weight(FeatureCategory.Genre, "Science Fiction"), 3);
        Assert.Equal(0.50, profile.Weight(FeatureCategory.Director, "Ridley Scott"), 3);
        Assert.Equal(-1.00, profile.Weight(FeatureCategory.Genre, "Musical"), 3);
        Assert.Equal(-1.00, profile.Weight(FeatureCategory.Genre, "Comedy"), 3);
        Assert.Equal(0.0, profile.Weight(FeatureCategory.Genre, "Thriller"), 3);
    }

    [Fact]
    public void Example56_TasteScores()
    {
        var request = ExampleRequest();
        var index = new FeatureIndex(request.Library, request.Settings);
        var profile = TasteProfile.Build(index, request.Ratings, request.NowUtc, request.Settings);

        var bladeRunner = TasteScorer.Score(index, profile, request.Library.Single(m => m.Name == "Blade Runner"), request.Settings);
        var musical = TasteScorer.Score(index, profile, request.Library.Single(m => m.Name == "Another Musical"), request.Settings);

        // Genres 1.0 x (0.5 + 0) / sqrt(2) = 0.354; direction 1.2 x 0.5 = 0.60; total 0.95.
        Assert.Equal(0.954, bladeRunner.Total, 3);
        Assert.Equal(0.95, Math.Round(bladeRunner.Total, 2));
        // 1.0 x (-1 - 1) / sqrt(2) = -1.41.
        Assert.Equal(-1.414, musical.Total, 3);
    }

    [Fact]
    public void Example56_BladeRunnerFirst_MusicalLast_WithExplanation()
    {
        var result = RecommendationEngine.Recommend(ExampleRequest());

        Assert.Equal(new[] { Id("Blade Runner"), Id("Another Musical") }, result.Items.Select(i => i.ItemId));
        var explanation = result.Items[0].Explanation;
        Assert.Equal(FeatureCategory.Director, explanation.TopFeatures[0].Category);
        Assert.Equal("Ridley Scott", explanation.TopFeatures[0].Value);
        Assert.Equal(0.6, explanation.TopFeatures[0].Contribution, 3);
        Assert.Equal("Science Fiction", explanation.TopFeatures[1].Value);
        Assert.Equal(Id("Alien"), explanation.BecauseOfItemId);
        Assert.Equal("Alien", explanation.BecauseOfName);
    }

    // ---------- Formulas ----------

    [Fact]
    public void Idf_FollowsTheFormula()
    {
        var library = Enumerable.Range(0, 10).Select(i => Movie("m" + i, i < 3 ? ["Rare"] : ["Common"])).ToList();
        var index = new FeatureIndex(library, new EngineSettings());

        Assert.Equal(Math.Log(10.0 / (1 + 3)) + 1, index.Idf(FeatureCategory.Genre, "Rare"), 9);
        Assert.Equal(Math.Log(10.0 / (1 + 7)) + 1, index.Idf(FeatureCategory.Genre, "Common"), 9);
    }

    [Fact]
    public void Decay_HalvesARatingEveryHalfLife()
    {
        var library = new List<MediaItem> { Movie("Old", ["Drama"]) };
        var settings = new EngineSettings { UseIdf = false, SmoothingK = 0, HalfLifeDays = 365 };
        var index = new FeatureIndex(library, settings);

        // With k = 0 a single rating gives P = v regardless of weight, so use two ratings on the same feature.
        library.Add(Movie("New", ["Drama"]));
        index = new FeatureIndex(library, settings);
        var profile = TasteProfile.Build(index, [Rate("Old", 3, Now.AddDays(-365)), Rate("New", -3, Now)], Now, settings);

        // (3 x 0.5 - 3 x 1) / (0.5 + 1) = -1.
        Assert.Equal(-1.0, profile.Weight(FeatureCategory.Genre, "Drama"), 6);
        Assert.Equal(0.5, TasteProfile.Decay(Now.AddDays(-365), Now, 365), 9);
        Assert.Equal(1.0, TasteProfile.Decay(Now.AddDays(-3650), Now, 0), 9);
    }

    [Fact]
    public void Limits_ActorsWritersStudios()
    {
        var item = new MediaItem
        {
            Id = Guid.NewGuid(),
            Actors = ["a1", "a2", "a3", "a4", "a5", "a6", "a7"],
            Writers = ["w1", "w2", "w3", "w4"],
            Studios = ["s1", "s2", "s3"],
            Year = 1987,
        };

        var features = FeatureExtractor.Extract(item, new EngineSettings());

        Assert.Equal(5, features.Count(f => f.Category == FeatureCategory.Actor));
        Assert.Equal(3, features.Count(f => f.Category == FeatureCategory.Writer));
        Assert.Equal(2, features.Count(f => f.Category == FeatureCategory.Studio));
        Assert.Contains(features, f => f.Category == FeatureCategory.Decade && f.Display == "1980s");
        Assert.DoesNotContain(features, f => f.Display == "a6");
    }

    // ---------- Rules ----------

    [Fact]
    public void RatedAndWatchedItems_AreNeverRecommended()
    {
        var library = Synthetic(300);
        var ratings = library.Take(40).Select((m, i) => new UserRating { ItemId = m.Id, Value = i % 2 == 0 ? 3 : -1, TimestampUtc = Now, IsExplicit = true, IsLoved = i % 2 == 0 }).ToList();
        var result = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now });

        var rated = ratings.Select(r => r.ItemId).ToHashSet();
        var played = library.Where(m => m.Played).Select(m => m.Id).ToHashSet();
        Assert.NotEmpty(result.Items);
        Assert.DoesNotContain(result.Items, i => rated.Contains(i.ItemId));
        Assert.DoesNotContain(result.Items, i => played.Contains(i.ItemId));
        Assert.Equal(library.Count(m => !m.Played && !rated.Contains(m.Id)), result.Items.Count);
    }

    [Fact]
    public void AHatedItem_NeverAppears_EvenWhenOtherwiseTheBestMatch()
    {
        var library = new List<MediaItem>
        {
            Movie("Loved", ["Sci-Fi"], ["Dir A"], rating: 9),
            Movie("Hated twin", ["Sci-Fi"], ["Dir A"], rating: 9.9),
            Movie("Other", ["Drama"], ["Dir B"], rating: 5),
        };
        var ratings = new List<UserRating> { Rate("Loved", 3), Rate("Hated twin", -3) };

        var result = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now });

        Assert.DoesNotContain(result.Items, i => i.ItemId == Id("Hated twin"));
    }

    [Fact]
    public void OtherChaptersOfAHatedSaga_AreExcluded_WhenTheOptionIsOn()
    {
        var library = new List<MediaItem>
        {
            Movie("Saga 1", ["Action"], collections: ["Saga"]),
            Movie("Saga 2", ["Action"], collections: ["Saga"]),
            Movie("Standalone", ["Action"]),
        };
        var ratings = new List<UserRating> { Rate("Saga 1", -3) };

        var on = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now });
        var off = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now, Settings = new EngineSettings { ExcludeHatedCollections = false } });

        Assert.DoesNotContain(on.Items, i => i.ItemId == Id("Saga 2"));
        Assert.Contains(off.Items, i => i.ItemId == Id("Saga 2"));
    }

    [Fact]
    public void ProximityPenalty_HalvesTheTasteOfItemsSharingDirectorWithAHatedItem()
    {
        var library = new List<MediaItem>
        {
            Movie("Hated", ["Horror"], ["Dir X"]),
            Movie("Loved", ["Comedy"], ["Dir Y"]),
            Movie("Same director", ["Comedy"], ["Dir X"]),
            Movie("Low", ["War"], ["Dir Z"]),
            Movie("High", ["Comedy"], ["Dir Y"]),
        };
        var ratings = new List<UserRating> { Rate("Hated", -3), Rate("Loved", 3) };
        var request = new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now };

        var penalized = RecommendationEngine.Recommend(request).Items.Single(i => i.ItemId == Id("Same director"));
        var unpenalized = RecommendationEngine.Recommend(request with { Settings = new EngineSettings { ProximityPenalty = 1.0 } }).Items.Single(i => i.ItemId == Id("Same director"));

        Assert.True(penalized.Penalized);
        Assert.Equal(unpenalized.Taste * 0.5, penalized.Taste, 9);
        Assert.False(RecommendationEngine.Recommend(request).Items.Single(i => i.ItemId == Id("High")).Penalized);
    }

    [Fact]
    public void AtMostTwoItemsOfTheSameSaga_InTheFirstTwenty()
    {
        var library = new List<MediaItem>();
        for (var i = 0; i < 8; i++)
        {
            library.Add(Movie("Saga " + i, ["Space"], ["Dir S"], collections: ["Star Saga"], rating: 9));
        }

        for (var i = 0; i < 40; i++)
        {
            library.Add(Movie("Filler " + i, ["Genre" + (i % 7)], ["Dir " + i], rating: 5));
        }

        library.Add(Movie("Liked", ["Space"], ["Dir S"]));
        var ratings = new List<UserRating> { Rate("Liked", 3) };

        var result = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now });

        var top20 = result.Items.Take(20).Select(i => i.ItemId).ToHashSet();
        Assert.Equal(2, library.Count(m => m.Collections.Contains("Star Saga") && top20.Contains(m.Id)));
        Assert.Contains("Star Saga", library.Single(m => m.Id == result.Items[0].ItemId).Collections);
        Assert.Equal(library.Count - 1, result.Items.Count);
    }

    [Fact]
    public void Mmr_PrefersADifferentItemOverANearDuplicate()
    {
        var candidates = new List<DiversityCandidate>
        {
            new(Id("A"), 1.00, new Dictionary<string, double> { ["g:x"] = 1, ["d:1"] = 1 }, []),
            new(Id("A copy"), 0.98, new Dictionary<string, double> { ["g:x"] = 1, ["d:1"] = 1 }, []),
            new(Id("B"), 0.90, new Dictionary<string, double> { ["g:y"] = 1, ["d:2"] = 1 }, []),
        };

        var mmr = Diversifier.Rerank(candidates, lambda: 0.8, poolSize: 50, maxPerCollection: 2, capWindow: 20);
        var plain = Diversifier.Rerank(candidates, lambda: 1.0, poolSize: 50, maxPerCollection: 2, capWindow: 20);

        Assert.Equal(new[] { Id("A"), Id("B"), Id("A copy") }, mmr);
        Assert.Equal(new[] { Id("A"), Id("A copy"), Id("B") }, plain);
    }

    // ---------- Exploration and determinism ----------

    [Fact]
    public void Exploration_IsStableWithinADay_AndChangesTheNextDay()
    {
        var library = Synthetic(200);
        var request = new RecommendationRequest { Library = library, Ratings = [], UserKey = "user-a", NowUtc = Now };

        var morning = RecommendationEngine.Recommend(request with { NowUtc = Now.Date.AddHours(1) });
        var evening = RecommendationEngine.Recommend(request with { NowUtc = Now.Date.AddHours(23) });
        var tomorrow = RecommendationEngine.Recommend(request with { NowUtc = Now.Date.AddDays(1).AddHours(1) });

        Assert.Equal(morning.Items.Select(i => i.ItemId), evening.Items.Select(i => i.ItemId));
        Assert.Equal(morning.Items.Select(i => i.Exploration), evening.Items.Select(i => i.Exploration));
        Assert.NotEqual(
            morning.Items.OrderBy(i => i.ItemId).Select(i => i.Exploration),
            tomorrow.Items.OrderBy(i => i.ItemId).Select(i => i.Exploration));
        Assert.All(morning.Items, i => Assert.InRange(i.Exploration, 0, 1));
    }

    [Fact]
    public void Exploration_DependsOnTheUser()
    {
        Assert.NotEqual(StableRandom.Unit("user-a", Now, Id("x")), StableRandom.Unit("user-b", Now, Id("x")));
        Assert.Equal(StableRandom.Unit("user-a", Now, Id("x")), StableRandom.Unit("user-a", Now.AddHours(3), Id("x")));
    }

    [Fact]
    public void SameInput_SameOutput_EvenWithShuffledLibrary()
    {
        var library = Synthetic(400);
        var ratings = library.Take(30).Select((m, i) => new UserRating { ItemId = m.Id, Value = (i % 4) - 1.5, TimestampUtc = Now.AddDays(-i), IsExplicit = true }).ToList();
        var a = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now });
        var shuffled = library.OrderBy(m => m.Name.GetHashCode(StringComparison.Ordinal) ^ 12345).ToList();
        var b = RecommendationEngine.Recommend(new RecommendationRequest { Library = shuffled, Ratings = ratings.AsEnumerable().Reverse().ToList(), UserKey = "u", NowUtc = Now });

        Assert.Equal(a.Items.Select(i => i.ItemId), b.Items.Select(i => i.ItemId));
        Assert.Equal(a.Items.Select(i => i.Score), b.Items.Select(i => i.Score));
    }

    // ---------- Cold start ----------

    [Fact]
    public void ColdStart_BelowFiveExplicitRatings_UsesColdWeights()
    {
        var library = Synthetic(100);
        var four = library.Take(4).Select(m => new UserRating { ItemId = m.Id, Value = 3, TimestampUtc = Now, IsExplicit = true, IsLoved = true }).ToList();
        var implicitToo = four.Concat(library.Skip(4).Take(3).Select(m => new UserRating { ItemId = m.Id, Value = 0.5, TimestampUtc = Now })).ToList();

        var cold = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = implicitToo, UserKey = "u", NowUtc = Now });

        Assert.True(cold.IsColdStart);
        Assert.Equal(4, cold.ExplicitRatingCount);
        Assert.All(cold.Items, i => Assert.Equal((0.3 * i.Taste) + (0.5 * i.Quality) + (0.2 * i.Exploration), i.Score, 9));

        var five = library.Take(5).Select(m => new UserRating { ItemId = m.Id, Value = 3, TimestampUtc = Now, IsExplicit = true, IsLoved = true }).ToList();
        var warm = RecommendationEngine.Recommend(new RecommendationRequest { Library = library, Ratings = five, UserKey = "u", NowUtc = Now });

        Assert.False(warm.IsColdStart);
        Assert.All(warm.Items, i => Assert.Equal((0.75 * i.Taste) + (0.15 * i.Quality) + (0.10 * i.Exploration), i.Score, 9));
    }

    [Fact]
    public void Onboarding_PrefersWatchedUnrated_ThenWellRated_AndCoversGenres()
    {
        var library = new List<MediaItem>
        {
            Movie("Watched drama", ["Drama"], rating: 6, played: true),
            Movie("Watched comedy", ["Comedy"], rating: 5, played: true),
            Movie("Rated watched", ["Horror"], rating: 9, played: true),
            Movie("Top drama", ["Drama"], rating: 9.5),
            Movie("Second drama", ["Drama"], rating: 9.4),
            Movie("Top western", ["Western"], rating: 8),
            Movie("War film", ["War"], rating: 7),
        };
        var ratings = new List<UserRating> { Rate("Rated watched", 1) };
        var request = new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now };

        var four = Onboarding.Select(request, 4);
        var six = Onboarding.Select(request, 6);

        // Watched-but-unrated first, then the best rated, one per genre before repeating a genre.
        Assert.Equal(new[] { Id("Watched drama"), Id("Watched comedy"), Id("Top western"), Id("War film") }, four);
        Assert.Equal(new[] { Id("Top drama"), Id("Second drama") }, six.Skip(4));
        Assert.DoesNotContain(Id("Rated watched"), six);
    }

    // ---------- Performance ----------

    [Fact]
    public void TwoThousandItems_AreRankedInUnderTwoSeconds()
    {
        var library = Synthetic(2000);
        var random = new Random(3);
        int[] levels = [-3, -1, 1, 3];
        var ratings = library.Where((_, i) => i % 13 == 0).Select(m => new UserRating
        {
            ItemId = m.Id,
            Value = levels[random.Next(4)],
            TimestampUtc = Now.AddDays(-random.Next(800)),
            IsExplicit = true,
        }).ToList();
        var request = new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "perf", NowUtc = Now };
        RecommendationEngine.Recommend(request with { Library = library.Take(50).ToList() }); // JIT warm-up

        var watch = Stopwatch.StartNew();
        var result = RecommendationEngine.Recommend(request);
        watch.Stop();

        Assert.True(result.Items.Count > 1500);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), "took " + watch.Elapsed);
    }
}
