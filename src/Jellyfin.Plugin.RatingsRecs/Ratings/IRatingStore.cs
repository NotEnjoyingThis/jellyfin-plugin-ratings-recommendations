using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Per-user rating store. This is the source of truth for ratings; native user data is only a mirror.
/// </summary>
public interface IRatingStore
{
    /// <summary>
    /// Raised after any change to a user's ratings.
    /// </summary>
    event EventHandler<RatingsChangedEventArgs>? RatingsChanged;

    /// <summary>
    /// Gets the rating of an item, or <c>null</c>.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>The rating or <c>null</c>.</returns>
    RatingEntry? Get(Guid userId, Guid itemId);

    /// <summary>
    /// Gets the ratings of several items; unrated items are absent from the result.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemIds">The items.</param>
    /// <returns>Ratings by item id.</returns>
    IReadOnlyDictionary<Guid, RatingEntry> GetMany(Guid userId, IEnumerable<Guid> itemIds);

    /// <summary>
    /// Gets all ratings of a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>All ratings.</returns>
    IReadOnlyList<RatingEntry> GetAll(Guid userId);

    /// <summary>
    /// Sets or changes an explicit rating (replaces an implicit one).
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="itemType">The item type.</param>
    /// <param name="value">The canonical level.</param>
    /// <param name="seriesId">The series id for seasons.</param>
    /// <returns>The stored rating.</returns>
    RatingEntry SetExplicit(Guid userId, Guid itemId, string itemType, int value, Guid? seriesId = null);

    /// <summary>
    /// Sets an implicit rating unless an explicit one exists.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="itemType">The item type.</param>
    /// <param name="value">The canonical level.</param>
    /// <param name="seriesId">The series id for seasons.</param>
    /// <returns>The stored rating, or <c>null</c> when an explicit rating exists.</returns>
    RatingEntry? SetImplicit(Guid userId, Guid itemId, string itemType, int value, Guid? seriesId = null);

    /// <summary>
    /// Removes the rating of an item, whatever its source.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>The removed rating, or <c>null</c>.</returns>
    RatingEntry? Remove(Guid userId, Guid itemId);

    /// <summary>
    /// Removes the rating of an item only if it is implicit.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="itemId">The item.</param>
    /// <returns><c>true</c> if an implicit rating was removed.</returns>
    bool RemoveImplicit(Guid userId, Guid itemId);

    /// <summary>
    /// Merges ratings (CSV import): the most recently updated wins; invalid entries are skipped;
    /// an implicit entry never replaces an explicit one.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="entries">The entries to merge.</param>
    /// <returns>Counts.</returns>
    MergeResult Merge(Guid userId, IEnumerable<RatingEntry> entries);

    /// <summary>
    /// Gets the preferences of a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>The preferences (defaults when never set).</returns>
    UserPreferences GetPreferences(Guid userId);

    /// <summary>
    /// Sets the preferences of a user. Ratings are never touched.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="preferences">The preferences.</param>
    void SetPreferences(Guid userId, UserPreferences preferences);
}
