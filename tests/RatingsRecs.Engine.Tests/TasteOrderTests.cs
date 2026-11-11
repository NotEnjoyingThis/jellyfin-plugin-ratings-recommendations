using System;
using System.Collections.Generic;
using System.Linq;
using RatingsRecs.Engine;
using Xunit;
using static RatingsRecs.Engine.Tests.EngineTestData;

namespace RatingsRecs.Engine.Tests;

public class TasteOrderTests
{
    private static EngineContext Context(List<MediaItem> library, List<UserRating> ratings)
        => EngineContext.Create(new RecommendationRequest { Library = library, Ratings = ratings, UserKey = "u", NowUtc = Now });

    [Fact]
    public void Order_PutsTheBestTasteFirst_AndDropsNegativelyRatedItems()
    {
        var library = new List<MediaItem>
        {
            Movie("Loved", ["Western"], ["Dir L"]),
            Movie("Same director", ["Drama"], ["Dir L"]),
            Movie("Neutral", ["Drama"], ["Dir N"]),
            Movie("Hated", ["Horror"], ["Dir H"]),
        };
        var context = Context(library, [Rate("Loved", 3), Rate("Hated", -3)]);

        var result = TasteOrder.Order(context, [Id("Neutral"), Id("Hated"), Id("Same director")], new Dictionary<Guid, double>());

        Assert.Equal(new[] { Id("Same director"), Id("Neutral") }, result.ToArray());
    }

    [Fact]
    public void Order_DropsRatingsGivenAfterTheComputation()
    {
        var library = new List<MediaItem> { Movie("A", ["X"]), Movie("B", ["Y"]) };
        var live = new Dictionary<Guid, double> { [Id("A")] = -1 };

        var result = TasteOrder.Order(Context(library, []), [Id("A"), Id("B")], live);

        Assert.Equal(new[] { Id("B") }, result.ToArray());
    }

    [Fact]
    public void Order_KeepsTheOriginalOrderForTies_AndUnknownItemsLast()
    {
        var library = new List<MediaItem> { Movie("A", ["X"]), Movie("B", ["Y"]), Movie("C", ["Z"]) };
        var unknown = Guid.NewGuid();

        var result = TasteOrder.Order(Context(library, []), [Id("C"), unknown, Id("A"), Id("B")], new Dictionary<Guid, double>());

        Assert.Equal(new[] { Id("C"), Id("A"), Id("B"), unknown }, result.ToArray());
    }
}
