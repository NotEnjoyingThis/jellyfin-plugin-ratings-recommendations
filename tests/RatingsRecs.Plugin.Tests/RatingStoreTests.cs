using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Microsoft.Extensions.Logging.Abstractions;
using RatingsRecs.Plugin.Tests.Support;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public sealed class RatingStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _dir = new();
    private readonly ManualTimeProvider _clock = new(Start);
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _item = Guid.NewGuid();

    public void Dispose() => _dir.Dispose();

    private RatingStore NewStore() => new(_dir.Path, NullLogger<RatingStore>.Instance, _clock);

    private string UserFile(Guid userId) => Path.Combine(_dir.Path, userId.ToString("D") + ".json");

    [Fact]
    public void SetExplicit_ThenGet_ReturnsTheRating()
    {
        var store = NewStore();

        var saved = store.SetExplicit(_user, _item, "Movie", RatingLevels.Amazing);
        var read = store.Get(_user, _item);

        Assert.NotNull(read);
        Assert.Equal(saved, read);
        Assert.Equal(RatingLevels.Amazing, read!.Value);
        Assert.Equal("Movie", read.ItemType);
        Assert.Equal(RatingSources.Explicit, read.Source);
        Assert.Equal(Start.UtcDateTime, read.CreatedUtc);
        Assert.Equal(Start.UtcDateTime, read.UpdatedUtc);
        Assert.Equal(DateTimeKind.Utc, read.CreatedUtc.Kind);
    }

    [Fact]
    public void Get_Unrated_ReturnsNull()
    {
        Assert.Null(NewStore().Get(_user, _item));
    }

    [Fact]
    public void ChangingTheRating_KeepsCreatedAndUpdatesUpdated()
    {
        var store = NewStore();
        store.SetExplicit(_user, _item, "Movie", RatingLevels.Good);
        _clock.Advance(TimeSpan.FromDays(3));

        var changed = store.SetExplicit(_user, _item, "Movie", RatingLevels.Hate);

        Assert.Equal(RatingLevels.Hate, changed.Value);
        Assert.Equal(Start.UtcDateTime, changed.CreatedUtc);
        Assert.Equal(Start.UtcDateTime.AddDays(3), changed.UpdatedUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-2)]
    [InlineData(4)]
    [InlineData(-4)]
    public void SetExplicit_InvalidLevel_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewStore().SetExplicit(_user, _item, "Movie", value));
    }

    [Fact]
    public void Ratings_SurviveARestart()
    {
        NewStore().SetExplicit(_user, _item, "Series", RatingLevels.Watchable);

        var reloaded = NewStore().Get(_user, _item);

        Assert.NotNull(reloaded);
        Assert.Equal(RatingLevels.Watchable, reloaded!.Value);
        Assert.Equal("Series", reloaded.ItemType);
    }

    [Fact]
    public void File_HasSchemaVersionAndDesignFieldNames()
    {
        var season = Guid.NewGuid();
        var series = Guid.NewGuid();
        var store = NewStore();
        store.SetExplicit(_user, _item, "Movie", RatingLevels.Good);
        store.SetExplicit(_user, season, "Season", RatingLevels.Amazing, series);

        using var doc = JsonDocument.Parse(File.ReadAllText(UserFile(_user)));
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        var ratings = root.GetProperty("ratings").EnumerateArray().ToList();
        Assert.Equal(2, ratings.Count);
        var movie = ratings.Single(r => r.GetProperty("itemType").GetString() == "Movie");
        foreach (var name in new[] { "itemId", "itemType", "value", "source", "createdUtc", "updatedUtc" })
        {
            Assert.True(movie.TryGetProperty(name, out _), "missing " + name);
        }

        Assert.False(movie.TryGetProperty("seriesId", out _));
        var seasonJson = ratings.Single(r => r.GetProperty("itemType").GetString() == "Season");
        Assert.Equal(series, seasonJson.GetProperty("seriesId").GetGuid());
    }

    [Fact]
    public void Writes_LeaveNoTemporaryFiles()
    {
        var store = NewStore();
        for (var i = 0; i < 5; i++)
        {
            store.SetExplicit(_user, Guid.NewGuid(), "Movie", RatingLevels.Good);
        }

        var files = Directory.GetFiles(_dir.Path).Select(Path.GetFileName).ToList();
        Assert.Equal(new[] { _user.ToString("D") + ".json" }, files);
    }

    [Fact]
    public void Users_AreIsolated()
    {
        var other = Guid.NewGuid();
        var store = NewStore();
        store.SetExplicit(_user, _item, "Movie", RatingLevels.Amazing);

        Assert.Null(store.Get(other, _item));
        Assert.Empty(store.GetAll(other));
        Assert.False(File.Exists(UserFile(other)));
    }

    [Fact]
    public void Implicit_NeverOverridesExplicit()
    {
        var store = NewStore();
        store.SetExplicit(_user, _item, "Movie", RatingLevels.Hate);

        var result = store.SetImplicit(_user, _item, "Movie", RatingLevels.Amazing);

        Assert.Null(result);
        Assert.Equal(RatingLevels.Hate, store.Get(_user, _item)!.Value);
        Assert.False(store.RemoveImplicit(_user, _item));
        Assert.NotNull(store.Get(_user, _item));
    }

    [Fact]
    public void Explicit_ReplacesImplicit()
    {
        var store = NewStore();
        store.SetImplicit(_user, _item, "Movie", RatingLevels.Amazing);
        Assert.Equal(RatingSources.Implicit, store.Get(_user, _item)!.Source);

        store.SetExplicit(_user, _item, "Movie", RatingLevels.Good);

        var entry = store.Get(_user, _item)!;
        Assert.Equal(RatingSources.Explicit, entry.Source);
        Assert.Equal(RatingLevels.Good, entry.Value);
    }

    [Fact]
    public void RemoveImplicit_RemovesOnlyImplicit()
    {
        var store = NewStore();
        store.SetImplicit(_user, _item, "Movie", RatingLevels.Amazing);

        Assert.True(store.RemoveImplicit(_user, _item));
        Assert.Null(store.Get(_user, _item));
    }

    [Fact]
    public void Remove_ReturnsTheRemovedEntry_OrNull()
    {
        var store = NewStore();
        store.SetExplicit(_user, _item, "Movie", RatingLevels.Good);

        var removed = store.Remove(_user, _item);

        Assert.NotNull(removed);
        Assert.Null(store.Get(_user, _item));
        Assert.Null(store.Remove(_user, _item));
        Assert.Null(NewStore().Get(_user, _item));
    }

    [Fact]
    public void GetMany_ReturnsOnlyRatedItems()
    {
        var store = NewStore();
        var rated = Guid.NewGuid();
        store.SetExplicit(_user, rated, "Movie", RatingLevels.Good);

        var result = store.GetMany(_user, new[] { rated, Guid.NewGuid(), Guid.NewGuid() });

        Assert.Single(result);
        Assert.True(result.ContainsKey(rated));
    }

    [Fact]
    public async Task ConcurrentWrites_AreAllKept()
    {
        var store = NewStore();
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        await Task.WhenAll(ids.Select(id => Task.Run(() => store.SetExplicit(_user, id, "Movie", RatingLevels.Good))));

        Assert.Equal(100, store.GetAll(_user).Count);
        Assert.Equal(100, NewStore().GetAll(_user).Count);
    }

    [Fact]
    public void CorruptFile_IsMovedAsideAndStoreStartsEmpty()
    {
        File.WriteAllText(UserFile(_user), "{ not json");
        var store = NewStore();

        Assert.Empty(store.GetAll(_user));
        store.SetExplicit(_user, _item, "Movie", RatingLevels.Good);

        Assert.Single(Directory.GetFiles(_dir.Path, "*.corrupt-*"));
        Assert.NotNull(NewStore().Get(_user, _item));
    }

    [Fact]
    public void NewerSchema_IsReadButNeverOverwritten()
    {
        var content = "{\"schemaVersion\":99,\"ratings\":[{\"itemId\":\"" + _item + "\",\"itemType\":\"Movie\",\"value\":3,\"source\":\"explicit\",\"createdUtc\":\"2026-01-01T00:00:00Z\",\"updatedUtc\":\"2026-01-01T00:00:00Z\"}],\"futureField\":true}";
        File.WriteAllText(UserFile(_user), content);
        var store = NewStore();

        Assert.Equal(RatingLevels.Amazing, store.Get(_user, _item)!.Value);
        Assert.Throws<InvalidOperationException>(() => store.SetExplicit(_user, Guid.NewGuid(), "Movie", RatingLevels.Good));
        Assert.Equal(content, File.ReadAllText(UserFile(_user)));
    }

    [Fact]
    public void Merge_NewerWins_OlderIsSkipped_NewIsAdded()
    {
        var store = NewStore();
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        store.SetExplicit(_user, older, "Movie", RatingLevels.Good);
        store.SetExplicit(_user, newer, "Movie", RatingLevels.Good);

        var t0 = Start.UtcDateTime;
        var result = store.Merge(_user, new List<RatingEntry>
        {
            new() { ItemId = older, ItemType = "Movie", Value = RatingLevels.Hate, Source = RatingSources.Explicit, CreatedUtc = t0.AddDays(-10), UpdatedUtc = t0.AddDays(-1) },
            new() { ItemId = newer, ItemType = "Movie", Value = RatingLevels.Amazing, Source = RatingSources.Explicit, CreatedUtc = t0.AddDays(-10), UpdatedUtc = t0.AddDays(1) },
            new() { ItemId = fresh, ItemType = "Series", Value = RatingLevels.Watchable, Source = RatingSources.Explicit, CreatedUtc = t0.AddDays(-5), UpdatedUtc = t0.AddDays(-5) },
            new() { ItemId = Guid.NewGuid(), ItemType = "Movie", Value = 7, Source = RatingSources.Explicit, CreatedUtc = t0, UpdatedUtc = t0 },
        });

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Updated);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(RatingLevels.Good, store.Get(_user, older)!.Value);
        var updated = store.Get(_user, newer)!;
        Assert.Equal(RatingLevels.Amazing, updated.Value);
        Assert.Equal(t0.AddDays(-10), updated.CreatedUtc);
        Assert.Equal(RatingLevels.Watchable, store.Get(_user, fresh)!.Value);
    }

    [Fact]
    public void RatingsChanged_IsRaisedOnEveryChange()
    {
        var store = NewStore();
        var events = new List<Guid>();
        store.RatingsChanged += (_, e) => events.Add(e.UserId);

        store.SetExplicit(_user, _item, "Movie", RatingLevels.Good);
        store.SetImplicit(_user, Guid.NewGuid(), "Movie", RatingLevels.Amazing);
        store.Remove(_user, _item);
        store.Remove(_user, _item);

        Assert.Equal(3, events.Count);
        Assert.All(events, id => Assert.Equal(_user, id));
    }
}
