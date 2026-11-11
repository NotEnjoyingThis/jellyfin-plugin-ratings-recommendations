using Jellyfin.Plugin.RatingsRecs.Override;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class SuggestionsOverrideTests
{
    [Theory]
    [InlineData("Suggestions", "GetSuggestions", OverrideTarget.Suggestions)]
    [InlineData("Suggestions", "GetSuggestionsLegacy", OverrideTarget.Suggestions)]
    [InlineData("Movies", "GetMovieRecommendations", OverrideTarget.MovieRecommendations)]
    [InlineData("Movies", "GetSimilarMovies", OverrideTarget.None)]
    [InlineData("Items", "GetItems", OverrideTarget.None)]
    [InlineData(null, null, OverrideTarget.None)]
    public void OnlyTheTwoEndpointsAreTargeted(string? controller, string? action, OverrideTarget expected)
    {
        Assert.Equal(expected, SuggestionsOverride.Match(controller, action));
    }

    [Theory]
    [InlineData(new string[0], true, null)]
    [InlineData(new[] { "Movie" }, true, "Movie")]
    [InlineData(new[] { "series" }, true, "Series")]
    [InlineData(new[] { "Movie,Series" }, true, null)]
    [InlineData(new[] { "Movie", "Series" }, true, null)]
    [InlineData(new[] { "Episode" }, false, null)]
    [InlineData(new[] { "Movie", "Audio" }, false, null)]
    public void SuggestionTypes_OnlyMoviesAndSeriesAreReplaced(string[] types, bool replace, string? kind)
    {
        var ok = SuggestionsOverride.TryGetKind(types, out var result);

        Assert.Equal(replace, ok);
        Assert.Equal(kind, result);
    }
}
