using System;
using System.Collections.Generic;
using System.Linq;
using RatingsRecs.Engine;

namespace RatingsRecs.Engine.Tests;

internal static class EngineTestData
{
    public static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    public static Guid Id(string name)
    {
        // Deterministic ids from names, so failures are readable and stable.
        var bytes = new byte[16];
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name));
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }

    public static MediaItem Movie(
        string name,
        string[]? genres = null,
        string[]? directors = null,
        string[]? actors = null,
        string[]? collections = null,
        double? rating = null,
        int? year = null,
        bool played = false,
        string[]? tags = null) => new()
        {
            Id = Id(name),
            Kind = "Movie",
            Name = name,
            Genres = genres ?? [],
            Directors = directors ?? [],
            Actors = actors ?? [],
            Collections = collections ?? [],
            Tags = tags ?? [],
            CommunityRating = rating,
            Year = year,
            Played = played,
        };

    public static UserRating Rate(string name, double value, DateTime? at = null) => new()
    {
        ItemId = Id(name),
        Value = value,
        TimestampUtc = at ?? Now,
        IsExplicit = true,
        IsHate = value <= -3,
        IsLoved = value > 0,
    };

    // Settings of the worked example of section 5.6: IDF = 1, k = 2, no decay.
    public static EngineSettings ExampleSettings() => new() { UseIdf = false, SmoothingK = 2, HalfLifeDays = 0 };

    // Synthetic library of "count" movies with overlapping features.
    public static List<MediaItem> Synthetic(int count, int seed = 7)
    {
        var random = new Random(seed);
        string Pick(string prefix, int max) => prefix + random.Next(max).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var items = new List<MediaItem>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new MediaItem
            {
                Id = Id("m" + i),
                Name = "Movie " + i,
                Genres = Enumerable.Range(0, 1 + random.Next(3)).Select(_ => Pick("genre", 18)).Distinct().ToList(),
                Directors = [Pick("director", 400)],
                Actors = Enumerable.Range(0, 8).Select(_ => Pick("actor", 1500)).Distinct().ToList(),
                Writers = Enumerable.Range(0, 2).Select(_ => Pick("writer", 600)).Distinct().ToList(),
                Tags = Enumerable.Range(0, random.Next(12)).Select(_ => Pick("tag", 300)).Distinct().ToList(),
                Studios = [Pick("studio", 80)],
                Collections = random.Next(10) == 0 ? [Pick("saga", 60)] : [],
                Year = 1950 + random.Next(75),
                CommunityRating = Math.Round(3 + (random.NextDouble() * 6), 1),
                Played = random.Next(10) == 0,
            });
        }

        return items;
    }
}
