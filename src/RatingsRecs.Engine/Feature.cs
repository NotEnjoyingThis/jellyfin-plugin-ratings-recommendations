using System;
using System.Globalization;

namespace RatingsRecs.Engine;

/// <summary>
/// One feature of an item: a category plus a normalised key (case-insensitive) and the display text.
/// </summary>
/// <param name="Category">The category.</param>
/// <param name="Key">The normalised key.</param>
/// <param name="Display">The display text.</param>
public readonly record struct Feature(FeatureCategory Category, string Key, string Display)
{
    /// <summary>
    /// Gets a key unique across categories (for vectors and dictionaries).
    /// </summary>
    public string FullKey => ((int)Category).ToString(CultureInfo.InvariantCulture) + "|" + Key;

    /// <summary>
    /// Creates a feature from a raw value.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The feature.</returns>
    public static Feature Of(FeatureCategory category, string value)
    {
        var display = (value ?? string.Empty).Trim();
        return new Feature(category, display.ToLowerInvariant(), display);
    }

    /// <summary>
    /// Builds the full key of a category and raw value.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The full key.</returns>
    public static string FullKeyOf(FeatureCategory category, string value) => Of(category, value).FullKey;

    /// <inheritdoc />
    public bool Equals(Feature other) => Category == other.Category && string.Equals(Key, other.Key, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Category, StringComparer.Ordinal.GetHashCode(Key));
}
