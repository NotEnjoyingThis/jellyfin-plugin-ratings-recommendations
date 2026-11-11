using System;
using Jellyfin.Plugin.RatingsRecs.Similar;
using MediaBrowser.Model.Entities;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class SimilarProviderOrderTests
{
    private const string Ours = SimilarProviderOrder.ProviderName;

    [Fact]
    public void Enable_EmptySettings_PutsOursAlone()
    {
        Assert.Equal(new[] { Ours }, SimilarProviderOrder.Apply([], [], enable: true));
    }

    [Fact]
    public void Enable_PutsOursFirst_KeepingTheExistingOrder()
    {
        Assert.Equal(
            new[] { Ours, "Local Genre/Tag", "TheMovieDb" },
            SimilarProviderOrder.Apply(["Local Genre/Tag", "TheMovieDb"], ["TheMovieDb"], enable: true));
    }

    [Fact]
    public void Enable_WithoutAnExplicitOrder_StartsFromTheEnabledProviders()
    {
        // Jellyfin orders by SimilarItemProviderOrder, or by SimilarItemProviders when the order is empty.
        Assert.Equal(new[] { Ours, "TheMovieDb" }, SimilarProviderOrder.Apply([], ["TheMovieDb"], enable: true));
    }

    [Fact]
    public void Enable_MovesOursToTheTop()
    {
        Assert.Equal(
            new[] { Ours, "Local Genre/Tag" },
            SimilarProviderOrder.Apply(["Local Genre/Tag", "ratings & RECOMMENDATIONS"], [], enable: true));
    }

    [Fact]
    public void Enable_AlreadyFirst_ChangesNothing()
    {
        Assert.Null(SimilarProviderOrder.Apply([Ours, "Local Genre/Tag"], [], enable: true));
    }

    [Fact]
    public void Disable_RemovesOursOnly()
    {
        Assert.Equal(new[] { "Local Genre/Tag", "TheMovieDb" }, SimilarProviderOrder.Apply([Ours, "Local Genre/Tag", "TheMovieDb"], [], enable: false));
        Assert.Equal(Array.Empty<string>(), SimilarProviderOrder.Apply([Ours], [], enable: false));
    }

    [Fact]
    public void Disable_WhenNotThere_ChangesNothing()
    {
        Assert.Null(SimilarProviderOrder.Apply([], ["TheMovieDb"], enable: false));
        Assert.Null(SimilarProviderOrder.Apply(["Local Genre/Tag"], [], enable: false));
    }

    [Fact]
    public void NullArrays_AreTreatedAsEmpty()
    {
        Assert.Equal(new[] { Ours }, SimilarProviderOrder.Apply(null, null, enable: true));
        Assert.Null(SimilarProviderOrder.Apply(null, null, enable: false));
    }

    [Theory]
    [InlineData(CollectionTypeOptions.movies, new[] { "Movie" })]
    [InlineData(CollectionTypeOptions.tvshows, new[] { "Series" })]
    [InlineData(CollectionTypeOptions.mixed, new[] { "Movie", "Series" })]
    [InlineData(null, new[] { "Movie", "Series" })]
    [InlineData(CollectionTypeOptions.music, new string[0])]
    [InlineData(CollectionTypeOptions.boxsets, new string[0])]
    public void ItemTypes_FollowTheLibraryType(CollectionTypeOptions? type, string[] expected)
    {
        Assert.Equal(expected, SimilarProviderOrder.ItemTypes(type));
    }
}
