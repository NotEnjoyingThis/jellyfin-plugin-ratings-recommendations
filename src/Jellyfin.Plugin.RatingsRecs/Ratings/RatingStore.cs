using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// JSON-file rating store: one file per user (<c>&lt;userId&gt;.json</c>), atomic writes
/// (temporary file + rename) and one lock per user. Files are cached in memory after the first read.
/// </summary>
public sealed class RatingStore : IRatingStore
{
    /// <summary>
    /// The schema version written by this code.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _directory;
    private readonly ILogger<RatingStore> _logger;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, UserRatings> _users = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingStore"/> class.
    /// </summary>
    /// <param name="directory">The directory holding one JSON file per user.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The clock.</param>
    public RatingStore(string directory, ILogger<RatingStore> logger, TimeProvider clock)
    {
        _directory = directory;
        _logger = logger;
        _clock = clock;
        Directory.CreateDirectory(directory);
    }

    /// <inheritdoc />
    public event EventHandler<RatingsChangedEventArgs>? RatingsChanged;

    /// <inheritdoc />
    public RatingEntry? Get(Guid userId, Guid itemId)
    {
        var user = Load(userId);
        var gate = user.Gate;
        lock (gate)
        {
            return user.Ratings.TryGetValue(itemId, out var entry) ? entry : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Guid, RatingEntry> GetMany(Guid userId, IEnumerable<Guid> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        var user = Load(userId);
        var result = new Dictionary<Guid, RatingEntry>();
        var gate = user.Gate;
        lock (gate)
        {
            foreach (var id in itemIds)
            {
                if (user.Ratings.TryGetValue(id, out var entry))
                {
                    result[id] = entry;
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<RatingEntry> GetAll(Guid userId)
    {
        var user = Load(userId);
        var gate = user.Gate;
        lock (gate)
        {
            return user.Ratings.Values.ToList();
        }
    }

    /// <inheritdoc />
    public RatingEntry SetExplicit(Guid userId, Guid itemId, string itemType, int value, Guid? seriesId = null)
    {
        ValidateLevel(value);
        var user = Load(userId);
        RatingEntry entry;
        var gate = user.Gate;
        lock (gate)
        {
            EnsureWritable(user);
            var now = _clock.GetUtcNow().UtcDateTime;
            user.Ratings.TryGetValue(itemId, out var existing);
            entry = new RatingEntry
            {
                ItemId = itemId,
                ItemType = itemType,
                Value = value,
                Source = RatingSources.Explicit,
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = now,
                SeriesId = seriesId,
            };
            user.Ratings[itemId] = entry;
            Save(userId, user);
        }

        OnChanged(userId);
        return entry;
    }

    /// <inheritdoc />
    public RatingEntry? SetImplicit(Guid userId, Guid itemId, string itemType, int value, Guid? seriesId = null)
    {
        ValidateLevel(value);
        var user = Load(userId);
        RatingEntry entry;
        var gate = user.Gate;
        lock (gate)
        {
            if (user.Ratings.TryGetValue(itemId, out var existing) && existing.IsExplicit)
            {
                return null;
            }

            if (existing is not null && existing.Value == value)
            {
                return existing;
            }

            EnsureWritable(user);
            var now = _clock.GetUtcNow().UtcDateTime;
            entry = new RatingEntry
            {
                ItemId = itemId,
                ItemType = itemType,
                Value = value,
                Source = RatingSources.Implicit,
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = now,
                SeriesId = seriesId,
            };
            user.Ratings[itemId] = entry;
            Save(userId, user);
        }

        OnChanged(userId);
        return entry;
    }

    /// <inheritdoc />
    public RatingEntry? Remove(Guid userId, Guid itemId)
    {
        var user = Load(userId);
        RatingEntry? removed;
        var gate = user.Gate;
        lock (gate)
        {
            if (!user.Ratings.TryGetValue(itemId, out removed))
            {
                return null;
            }

            EnsureWritable(user);
            user.Ratings.Remove(itemId);
            Save(userId, user);
        }

        OnChanged(userId);
        return removed;
    }

    /// <inheritdoc />
    public bool RemoveImplicit(Guid userId, Guid itemId)
    {
        var user = Load(userId);
        var gate = user.Gate;
        lock (gate)
        {
            if (!user.Ratings.TryGetValue(itemId, out var existing) || existing.IsExplicit)
            {
                return false;
            }

            EnsureWritable(user);
            user.Ratings.Remove(itemId);
            Save(userId, user);
        }

        OnChanged(userId);
        return true;
    }

    /// <inheritdoc />
    public MergeResult Merge(Guid userId, IEnumerable<RatingEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var user = Load(userId);
        int added = 0, updated = 0, skipped = 0;
        var gate = user.Gate;
        lock (gate)
        {
            EnsureWritable(user);
            foreach (var incoming in entries)
            {
                if (!RatingLevels.IsValid(incoming.Value) || incoming.ItemId == Guid.Empty || !IsKnownSource(incoming.Source))
                {
                    skipped++;
                    continue;
                }

                var normalized = incoming with
                {
                    CreatedUtc = DateTime.SpecifyKind(incoming.CreatedUtc, DateTimeKind.Utc),
                    UpdatedUtc = DateTime.SpecifyKind(incoming.UpdatedUtc, DateTimeKind.Utc),
                };

                if (!user.Ratings.TryGetValue(normalized.ItemId, out var existing))
                {
                    user.Ratings[normalized.ItemId] = normalized;
                    added++;
                    continue;
                }

                var implicitOverExplicit = existing.IsExplicit && !normalized.IsExplicit;
                if (implicitOverExplicit || normalized.UpdatedUtc <= existing.UpdatedUtc)
                {
                    skipped++;
                    continue;
                }

                user.Ratings[normalized.ItemId] = normalized with
                {
                    CreatedUtc = existing.CreatedUtc < normalized.CreatedUtc ? existing.CreatedUtc : normalized.CreatedUtc,
                };
                updated++;
            }

            if (added + updated > 0)
            {
                Save(userId, user);
            }
        }

        if (added + updated > 0)
        {
            OnChanged(userId);
        }

        return new MergeResult(added, updated, skipped);
    }

    /// <inheritdoc />
    public UserPreferences GetPreferences(Guid userId)
    {
        var user = Load(userId);
        var gate = user.Gate;
        lock (gate)
        {
            return user.Preferences;
        }
    }

    /// <inheritdoc />
    public void SetPreferences(Guid userId, UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var user = Load(userId);
        var gate = user.Gate;
        lock (gate)
        {
            EnsureWritable(user);
            user.Preferences = preferences;
            Save(userId, user);
        }

        OnChanged(userId);
    }

    private static bool IsKnownSource(string source)
        => source is RatingSources.Explicit or RatingSources.Implicit;

    private static void ValidateLevel(int value)
    {
        if (!RatingLevels.IsValid(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Rating must be -3, -1, 1 or 3.");
        }
    }

    private static void EnsureWritable(UserRatings user)
    {
        if (user.ReadOnlyReason is not null)
        {
            throw new InvalidOperationException(user.ReadOnlyReason);
        }
    }

    private string FilePath(Guid userId) => Path.Combine(_directory, userId.ToString("D", CultureInfo.InvariantCulture) + ".json");

    private UserRatings Load(Guid userId)
    {
        return _users.GetOrAdd(userId, ReadFromDisk);
    }

    private UserRatings ReadFromDisk(Guid userId)
    {
        var user = new UserRatings();
        var path = FilePath(userId);
        if (!File.Exists(path))
        {
            return user;
        }

        try
        {
            var file = JsonSerializer.Deserialize<RatingFile>(File.ReadAllText(path), JsonOptions)
                ?? throw new JsonException("Empty rating file.");
            if (file.SchemaVersion > CurrentSchemaVersion)
            {
                user.ReadOnlyReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Rating file {0} has schema version {1}, newer than supported {2}: it is read-only. Update the plugin.",
                    path,
                    file.SchemaVersion,
                    CurrentSchemaVersion);
                _logger.LogWarning("{Reason}", user.ReadOnlyReason);
            }

            user.Preferences = file.Preferences ?? new UserPreferences();
            foreach (var entry in file.Ratings ?? new List<RatingEntry>())
            {
                user.Ratings[entry.ItemId] = entry with
                {
                    CreatedUtc = DateTime.SpecifyKind(entry.CreatedUtc, DateTimeKind.Utc),
                    UpdatedUtc = DateTime.SpecifyKind(entry.UpdatedUtc, DateTimeKind.Utc),
                };
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            var aside = path + ".corrupt-" + _clock.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            _logger.LogError(ex, "Rating file {Path} is unreadable; moved to {Aside} and starting empty for this user", path, aside);
            File.Move(path, aside, true);
            user.Ratings.Clear();
        }

        return user;
    }

    private void Save(Guid userId, UserRatings user)
    {
        var path = FilePath(userId);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var file = new RatingFile
        {
            SchemaVersion = CurrentSchemaVersion,
            UserId = userId,
            Ratings = user.Ratings.Values.OrderBy(r => r.CreatedUtc).ThenBy(r => r.ItemId).ToList(),
            Preferences = user.Preferences.SeriesMode is null ? null : user.Preferences,
        };

        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(file, JsonOptions));
            File.Move(temp, path, true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete temporary file {Path}", path);
        }
    }

    private void OnChanged(Guid userId)
    {
        try
        {
            RatingsChanged?.Invoke(this, new RatingsChangedEventArgs(userId));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "A RatingsChanged handler failed");
        }
    }

    private sealed class UserRatings
    {
        public object Gate { get; } = new();

        public Dictionary<Guid, RatingEntry> Ratings { get; } = new();

        public string? ReadOnlyReason { get; set; }

        public UserPreferences Preferences { get; set; } = new();
    }

    private sealed class RatingFile
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("ratings")]
        public List<RatingEntry>? Ratings { get; set; }

        [JsonPropertyName("preferences")]
        public UserPreferences? Preferences { get; set; }
    }
}
