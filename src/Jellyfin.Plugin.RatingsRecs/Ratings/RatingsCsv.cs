using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// CSV export/import of ratings (RFC 4180: comma separator, double-quote escaping, CRLF lines).
/// </summary>
public static class RatingsCsv
{
    /// <summary>
    /// The exported columns, in order.
    /// </summary>
    public static readonly IReadOnlyList<string> Columns =
        ["itemId", "itemType", "value", "source", "createdUtc", "updatedUtc", "seriesId", "name", "year", "imdbId", "tmdbId", "tvdbId"];

    private static readonly string[] AcceptedTypes = ["Movie", "Series", "Season"];

    /// <summary>
    /// Writes rows to CSV text.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The CSV text.</returns>
    public static string Write(IEnumerable<RatingCsvRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Columns)).Append("\r\n");
        foreach (var row in rows)
        {
            var e = row.Entry;
            var fields = new[]
            {
                e.ItemId == Guid.Empty ? string.Empty : e.ItemId.ToString("D", CultureInfo.InvariantCulture),
                e.ItemType,
                e.Value.ToString(CultureInfo.InvariantCulture),
                e.Source,
                FormatDate(e.CreatedUtc),
                FormatDate(e.UpdatedUtc),
                e.SeriesId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty,
                row.Name ?? string.Empty,
                row.Year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.ImdbId ?? string.Empty,
                row.TmdbId ?? string.Empty,
                row.TvdbId ?? string.Empty,
            };
            sb.Append(string.Join(',', fields.Select(Quote))).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses CSV text. Columns are matched by header name (any order, case-insensitive).
    /// Invalid lines are reported and skipped.
    /// </summary>
    /// <param name="text">The CSV text.</param>
    /// <returns>Rows and errors.</returns>
    public static CsvParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rows = new List<RatingCsvRow>();
        var errors = new List<CsvError>();
        var records = ReadRecords(text.TrimStart('﻿')).ToList();
        if (records.Count == 0)
        {
            errors.Add(new CsvError(1, "Empty file."));
            return new CsvParseResult(rows, errors);
        }

        var header = records[0].Fields.Select(f => f.Trim()).ToList();
        int Col(string name) => header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        int iId = Col("itemId"), iType = Col("itemType"), iValue = Col("value"), iSource = Col("source"),
            iCreated = Col("createdUtc"), iUpdated = Col("updatedUtc"), iSeries = Col("seriesId"), iName = Col("name"),
            iYear = Col("year"), iImdb = Col("imdbId"), iTmdb = Col("tmdbId"), iTvdb = Col("tvdbId");

        if (iType < 0 || iValue < 0 || (iId < 0 && iName < 0))
        {
            errors.Add(new CsvError(records[0].Line, "Missing required columns: itemType, value and itemId or name."));
            return new CsvParseResult(rows, errors);
        }

        foreach (var record in records.Skip(1))
        {
            if (record.Fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Get(int index) => index >= 0 && index < record.Fields.Count ? record.Fields[index].Trim() : string.Empty;

            var error = TryBuildRow(Get, iId, iType, iValue, iSource, iCreated, iUpdated, iSeries, iName, iYear, iImdb, iTmdb, iTvdb, out var row);
            if (error is null)
            {
                rows.Add(row!);
            }
            else
            {
                errors.Add(new CsvError(record.Line, error));
            }
        }

        return new CsvParseResult(rows, errors);
    }

    private static string? TryBuildRow(
        Func<int, string> get,
        int iId,
        int iType,
        int iValue,
        int iSource,
        int iCreated,
        int iUpdated,
        int iSeries,
        int iName,
        int iYear,
        int iImdb,
        int iTmdb,
        int iTvdb,
        out RatingCsvRow? row)
    {
        row = null;
        var idText = get(iId);
        var itemId = Guid.Empty;
        if (idText.Length > 0 && !Guid.TryParse(idText, out itemId))
        {
            return "Invalid itemId '" + idText + "'.";
        }

        var name = get(iName);
        if (itemId == Guid.Empty && name.Length == 0)
        {
            return "Either itemId or name is required.";
        }

        var type = AcceptedTypes.FirstOrDefault(t => string.Equals(t, get(iType), StringComparison.OrdinalIgnoreCase));
        if (type is null)
        {
            return "Invalid itemType '" + get(iType) + "' (Movie, Series or Season).";
        }

        if (!int.TryParse(get(iValue), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) || !RatingLevels.IsValid(value))
        {
            return "Invalid value '" + get(iValue) + "' (-3, -1, 1 or 3).";
        }

        var sourceText = get(iSource);
        var source = sourceText.Length == 0 ? RatingSources.Explicit : sourceText.ToLowerInvariant();
        if (source is not (RatingSources.Explicit or RatingSources.Implicit))
        {
            return "Invalid source '" + sourceText + "'.";
        }

        if (!TryParseDate(get(iCreated), out var created) || !TryParseDate(get(iUpdated), out var updated))
        {
            return "Invalid date (use ISO 8601 UTC, e.g. 2026-01-31T20:00:00Z).";
        }

        var seriesText = get(iSeries);
        Guid? seriesId = null;
        if (seriesText.Length > 0)
        {
            if (!Guid.TryParse(seriesText, out var parsedSeries))
            {
                return "Invalid seriesId '" + seriesText + "'.";
            }

            seriesId = parsedSeries;
        }

        int? year = null;
        if (get(iYear).Length > 0)
        {
            if (!int.TryParse(get(iYear), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear))
            {
                return "Invalid year '" + get(iYear) + "'.";
            }

            year = parsedYear;
        }

        row = new RatingCsvRow
        {
            Entry = new RatingEntry
            {
                ItemId = itemId,
                ItemType = type,
                Value = value,
                Source = source,
                CreatedUtc = created ?? updated ?? DateTime.MinValue,
                UpdatedUtc = updated ?? created ?? DateTime.MinValue,
                SeriesId = seriesId,
            },
            Name = name.Length == 0 ? null : name,
            Year = year,
            ImdbId = NullIfEmpty(get(iImdb)),
            TmdbId = NullIfEmpty(get(iTmdb)),
            TvdbId = NullIfEmpty(get(iTvdb)),
        };
        return null;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static bool TryParseDate(string text, out DateTime? value)
    {
        value = null;
        if (text.Length == 0)
        {
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            value = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    private static string FormatDate(DateTime value)
        => value == DateTime.MinValue ? string.Empty : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Quote(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return field;
        }

        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static IEnumerable<CsvRecord> ReadRecords(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    yield return new CsvRecord(recordLine, fields);
                    fields = new List<string>();
                    line++;
                    recordLine = line;
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return new CsvRecord(recordLine, fields);
        }
    }

    private sealed record CsvRecord(int Line, List<string> Fields);
}
