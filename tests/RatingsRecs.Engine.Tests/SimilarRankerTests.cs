using System;
using System.Collections.Generic;
using System.Linq;
using RatingsRecs.Engine;
using Xunit;
using static RatingsRecs.Engine.Tests.EngineTestData;

namespace RatingsRecs.Engine.Tests;

public class SimilarRankerTests
{
    private static EngineContext Context(List<MediaItem> library, List<UserRating> ratings)
        => EngineContext.Create(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now });

    [Fact]
    public void OnlyItemsSharingFeatures_NeverTheSource_NeverNegativelyRated()
    {
        var library = new List<MediaItem>
        {
            Movie("Source", ["Sci-Fi"], ["Dir A"]),
            Movie("Close", ["Sci-Fi"], ["Dir A"]),
            Movie("Hated close", ["Sci-Fi"], ["Dir A"]),
            Movie("Meh close", ["Sci-Fi"]),
            Movie("Unrelated", ["Romance"], ["Dir R"]),
        };
        var context = Context(library, [Rate("Hated close", -3), Rate("Meh close", -1)]);

        var result = SimilarRanker.Rank(context, Id("Source"), 12).Select(s => s.ItemId).ToList();

        Assert.Equal(new[] { Id("Close") }, result);
    }

    [Fact]
    public void Candidates_AreOrderedByTasteNotOnlyByOverlap()
    {
        var library = new List<MediaItem>
        {
            Movie("Source", ["Western", "Drama"], ["Dir S"]),
            Movie("Twin", ["Western", "Drama"], ["Dir T"], rating: 7),
            Movie("Liked director's", ["Western"], ["Dir L"], rating: 7),
            Movie("Liked before", ["Comedy"], ["Dir L"]),
        };
        for (var i = 0; i < 6; i++)
        {
            library.Add(Movie("Other " + i, ["Genre" + i], ["D" + i]));
        }

        var ratings = new List<UserRating> { Rate("Liked before", 3) };
        for (var i = 0; i < 5; i++)
        {
            ratings.Add(Rate("Other " + i, 1));
        }

        var result = SimilarRanker.Rank(Context(library, ratings), Id("Source"), 12).Select(s => s.ItemId).ToList();

        Assert.Equal(Id("Liked director's"), result[0]);
        Assert.Contains(Id("Twin"), result);
    }

    [Fact]
    public void OnlyThePoolOfMostOverlappingItemsIsScored()
    {
        var library = new List<MediaItem> { Movie("Source", ["A", "B", "C"]) };
        for (var i = 0; i < 10; i++)
        {
            library.Add(Movie("Two " + i, ["A", "B"]));
        }

        library.Add(Movie("One but loved", ["C"], ["Star"]));
        library.Add(Movie("Loved star film", ["X"], ["Star"]));
        var context = Context(library, [Rate("Loved star film", 3)]);

        var small = SimilarRanker.Rank(context, Id("Source"), 20, poolSize: 10);
        var large = SimilarRanker.Rank(context, Id("Source"), 20, poolSize: 200);

        Assert.DoesNotContain(small, s => s.ItemId == Id("One but loved"));
        Assert.Equal(Id("One but loved"), large[0].ItemId);
    }

    [Fact]
    public void UnknownSource_GivesAnEmptyList()
    {
        var context = Context([Movie("A", ["X"])], []);

        Assert.Empty(SimilarRanker.Rank(context, Guid.NewGuid(), 12));
        Assert.Empty(SimilarRanker.OrderForProvider(context, Guid.NewGuid(), new Dictionary<Guid, double>()));
    }

    [Fact]
    public void Rank_WithKind_KeepsOnlyThatKind()
    {
        var library = new List<MediaItem>
        {
            Movie("Source", ["Sci-Fi"], ["Dir A"]),
            Movie("Film twin", ["Sci-Fi"], ["Dir A"]),
            Series("Show twin", ["Sci-Fi"]),
        };

        var result = SimilarRanker.Rank(Context(library, []), Id("Source"), 12, kind: "Movie").Select(s => s.ItemId).ToList();

        Assert.Equal(new[] { Id("Film twin") }, result);
    }

    [Fact]
    public void OrderForProvider_SimilarFirst_ThenOtherItemsOfTheSameKind_ThenNegativesLast()
    {
        var library = new List<MediaItem>
        {
            Movie("Source", ["Sci-Fi"], ["Dir A"]),
            Movie("Close", ["Sci-Fi"], ["Dir A"]),
            Movie("Closer by taste", ["Sci-Fi"], ["Dir L"]),
            Movie("Unrelated liked style", ["Romance"], ["Dir L"]),
            Movie("Unrelated", ["Western"], ["Dir W"]),
            Movie("Hated", ["Sci-Fi"], ["Dir A"]),
            Movie("Meh", ["Sci-Fi"], ["Dir A"]),
            Movie("Loved before", ["Comedy"], ["Dir L"]),
            Series("Show", ["Sci-Fi"]),
        };
        var context = Context(library, [Rate("Loved before", 3), Rate("Hated", -3), Rate("Meh", -1)]);

        var result = SimilarRanker.OrderForProvider(context, Id("Source"), new Dictionary<Guid, double>()).ToList();

        // Phase 1: items sharing features (ranked like SimilarRanker.Rank).
        Assert.Equal(new[] { Id("Close"), Id("Closer by taste") }.OrderBy(g => g), result.Take(2).OrderBy(g => g));

        // Phase 2: the other movies, best taste first (the liked director wins); positively rated items are allowed.
        Assert.Equal(new[] { Id("Loved before"), Id("Unrelated liked style"), Id("Unrelated") }.OrderBy(g => g), result.Skip(2).Take(3).OrderBy(g => g));
        Assert.True(result.IndexOf(Id("Unrelated liked style")) < result.IndexOf(Id("Unrelated")));

        // Phase 3: negatively rated movies last, the least disliked first; never the source, never another kind.
        Assert.Equal(new[] { Id("Meh"), Id("Hated") }, result.Skip(5).ToArray());
        Assert.DoesNotContain(Id("Source"), result);
        Assert.DoesNotContain(Id("Show"), result);
    }

    [Fact]
    public void OrderForProvider_RatingsGivenAfterTheComputation_AlsoGoLast()
    {
        var library = new List<MediaItem>
        {
            Movie("Source", ["Sci-Fi"], ["Dir A"]),
            Movie("Close", ["Sci-Fi"], ["Dir A"]),
            Movie("Just hated", ["Sci-Fi"], ["Dir A"]),
            Movie("Unrelated", ["Western"]),
        };
        var context = Context(library, []);
        var live = new Dictionary<Guid, double> { [Id("Just hated")] = -3 };

        var result = SimilarRanker.OrderForProvider(context, Id("Source"), live).ToList();

        Assert.Equal(new[] { Id("Close"), Id("Unrelated"), Id("Just hated") }, result.ToArray());
    }

    [Fact]
    public void OrderForProvider_SeriesSourceGivesSeries()
    {
        var library = new List<MediaItem>
        {
            Series("Source show", ["Drama"]),
            Series("Other show", ["Drama"]),
            Series("Far show", ["Cooking"]),
            Movie("Drama film", ["Drama"]),
        };

        var result = SimilarRanker.OrderForProvider(Context(library, []), Id("Source show"), new Dictionary<Guid, double>()).ToList();

        Assert.Equal(new[] { Id("Other show"), Id("Far show") }, result.ToArray());
    }

    private static MediaItem Series(string name, string[] genres) => new()
    {
        Id = Id(name),
        Kind = "Series",
        Name = name,
        Genres = genres,
    };
}
