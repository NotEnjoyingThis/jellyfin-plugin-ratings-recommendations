using System;
using System.Linq;
using Jellyfin.Plugin.RatingsRecs.Ratings;
using Xunit;

namespace RatingsRecs.Plugin.Tests;

public class RatingsCsvTests
{
    private static readonly DateTime Created = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateTime Updated = new(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private static RatingCsvRow Row(string name, int value = RatingLevels.Good, string type = "Movie") => new()
    {
        Entry = new RatingEntry
        {
            ItemId = Guid.NewGuid(),
            ItemType = type,
            Value = value,
            Source = RatingSources.Explicit,
            CreatedUtc = Created,
            UpdatedUtc = Updated,
        },
        Name = name,
        Year = 1982,
        ImdbId = "tt0000001",
        TmdbId = "42",
    };

    [Fact]
    public void WriteThenParse_RoundTrips_IncludingQuotesCommasAndNewlines()
    {
        var season = Row("Season 2", RatingLevels.Amazing, "Season") with { };
        season = season with { Entry = season.Entry with { SeriesId = Guid.NewGuid() } };
        var rows = new[] { Row("Plain"), Row("Comma, \"quoted\"\nand newline", RatingLevels.Hate), season };

        var parsed = RatingsCsv.Parse(RatingsCsv.Write(rows));

        Assert.Empty(parsed.Errors);
        Assert.Equal(rows.Length, parsed.Rows.Count);
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(rows[i].Entry, parsed.Rows[i].Entry);
            Assert.Equal(rows[i].Name, parsed.Rows[i].Name);
            Assert.Equal(rows[i].Year, parsed.Rows[i].Year);
            Assert.Equal(rows[i].ImdbId, parsed.Rows[i].ImdbId);
            Assert.Equal(rows[i].TmdbId, parsed.Rows[i].TmdbId);
        }
    }

    [Fact]
    public void Write_StartsWithTheDocumentedHeader()
    {
        var csv = RatingsCsv.Write(Array.Empty<RatingCsvRow>());

        Assert.StartsWith("itemId,itemType,value,source,createdUtc,updatedUtc,seriesId,name,year,imdbId,tmdbId,tvdbId", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_AcceptsAnyColumnOrder_BomAndBlankLines()
    {
        var id = Guid.NewGuid();
        var csv = "﻿value,ItemType,itemId\r\n\r\n-1,Series," + id + "\r\n";

        var parsed = RatingsCsv.Parse(csv);

        Assert.Empty(parsed.Errors);
        var row = Assert.Single(parsed.Rows);
        Assert.Equal(id, row.Entry.ItemId);
        Assert.Equal(RatingLevels.Watchable, row.Entry.Value);
        Assert.Equal("Series", row.Entry.ItemType);
        Assert.Equal(RatingSources.Explicit, row.Entry.Source);
    }

    [Fact]
    public void Parse_ReportsBadLinesAndKeepsTheGoodOnes()
    {
        var csv = "itemId,itemType,value\n"
            + Guid.NewGuid() + ",Movie,3\n"
            + Guid.NewGuid() + ",Movie,5\n"
            + "not-a-guid,Movie,1\n"
            + Guid.NewGuid() + ",Episode,1\n";

        var parsed = RatingsCsv.Parse(csv);

        Assert.Single(parsed.Rows);
        Assert.Equal(3, parsed.Errors.Count);
        Assert.Equal(new[] { 3, 4, 5 }, parsed.Errors.Select(e => e.Line));
    }

    [Fact]
    public void Parse_RowWithoutIdButWithName_IsKeptForMatching()
    {
        var parsed = RatingsCsv.Parse("itemType,value,name,year\nMovie,1,Neon Rain,1983\n");

        var row = Assert.Single(parsed.Rows);
        Assert.Equal(Guid.Empty, row.Entry.ItemId);
        Assert.Equal("Neon Rain", row.Name);
        Assert.Equal(1983, row.Year);
    }

    [Fact]
    public void Parse_MissingRequiredColumns_IsAnError()
    {
        var parsed = RatingsCsv.Parse("foo,bar\n1,2\n");

        Assert.Empty(parsed.Rows);
        Assert.Single(parsed.Errors);
        Assert.Equal(1, parsed.Errors[0].Line);
    }
}
