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
    }
}
