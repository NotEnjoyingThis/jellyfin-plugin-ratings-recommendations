using System;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Microsoft.Extensions.Logging.Abstractions;
using RatingsRecs.Plugin.Tests.Support;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public sealed class NativeMirrorAndSignalsTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _item = Guid.NewGuid();

    public void Dispose() => _dir.Dispose();

    private RatingStore NewStore() => new(_dir.Path, NullLogger<RatingStore>.Instance, TimeProvider.System);

    // Table of docs/DESIGN.md section 4: Likes is derived by Jellyfin from Rating (>= 6.5).
    [Theory]
    [InlineData(-3, 2.0, false, false)]
    [InlineData(-1, 4.0, false, false)]
    [InlineData(1, 7.0, true, null)]
    [InlineData(3, 10.0, true, true)]
    public void MirrorPlan_MatchesTheDesignTable(int level, double rating, bool likes, bool? favorite)
    {
        var plan = NativeMirrorPlan.For(level, amazingSetsFavorite: true);

        Assert.Equal(rating, plan.Rating);
        Assert.Equal(likes, plan.Rating >= 6.5);
        Assert.Equal(favorite, plan.IsFavorite);
    }

    [Fact]
    public void MirrorPlan_NoRating_ClearsRatingAndLeavesFavourite()
    {
        var plan = NativeMirrorPlan.For(null, amazingSetsFavorite: true);

        Assert.Null(plan.Rating);
        Assert.Null(plan.IsFavorite);
    }

    [Fact]
    public void MirrorPlan_AmazingWithoutFavouriteOption_LeavesFavourite()
    {
        Assert.Null(NativeMirrorPlan.For(RatingLevels.Amazing, amazingSetsFavorite: false).IsFavorite);
    }

    [Fact]
    public void SelfWriteGuard_IsActiveOnlyInsideTheScope_AndNests()
    {
        var guard = new SelfWriteGuard();
        Assert.False(guard.IsActive(_user, _item));

        using (guard.Enter(_user, _item))
        {
            Assert.True(guard.IsActive(_user, _item));
            Assert.False(guard.IsActive(Guid.NewGuid(), _item));
            using (guard.Enter(_user, _item))
            {
                Assert.True(guard.IsActive(_user, _item));
            }

            Assert.True(guard.IsActive(_user, _item));
        }

        Assert.False(guard.IsActive(_user, _item));
    }

    [Fact]
    public void FavouriteFromOtherClient_WithoutRating_BecomesImplicitAmazing()
    {
        var store = NewStore();

        Assert.True(ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", isFavorite: true, enabled: true));

        var entry = store.Get(_user, _item)!;
        Assert.Equal(RatingLevels.Amazing, entry.Value);
        Assert.Equal(RatingSources.Implicit, entry.Source);
    }

    [Fact]
    public void Unfavourite_RemovesTheImplicitRating()
    {
        var store = NewStore();
        ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", true, true);

        Assert.True(ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", false, true));
        Assert.Null(store.Get(_user, _item));
    }

    [Fact]
    public void FavouriteChanges_NeverTouchAnExplicitRating()
    {
        var store = NewStore();
        store.SetExplicit(_user, _item, "Movie", RatingLevels.Hate);

        Assert.False(ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", true, true));
        Assert.False(ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", false, true));
        Assert.Equal(RatingLevels.Hate, store.Get(_user, _item)!.Value);
        Assert.True(store.Get(_user, _item)!.IsExplicit);
    }

    [Fact]
    public void FavouriteRule_Disabled_DoesNothing()
    {
        var store = NewStore();

        Assert.False(ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", true, false));
        Assert.Null(store.Get(_user, _item));
    }

    [Fact]
    public void RepeatedFavourite_DoesNotRewrite()
    {
        var store = NewStore();
        ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", true, true);
        var changes = 0;
        store.RatingsChanged += (_, _) => changes++;

        Assert.False(ImplicitFavoriteRule.Apply(store, _user, _item, "Movie", true, true));
        Assert.Equal(0, changes);
    }
}
