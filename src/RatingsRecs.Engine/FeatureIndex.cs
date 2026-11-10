using System;
using System.Collections.Generic;
using System.Linq;

namespace RatingsRecs.Engine;

/// <summary>
/// Features of every library item and their document frequency (for the IDF of section 5.1).
/// </summary>
public sealed class FeatureIndex
{
    private readonly Dictionary<Guid, IReadOnlyList<Feature>> _features = new();
    private readonly Dictionary<Guid, MediaItem> _items = new();
    private readonly Dictionary<Feature, int> _documentFrequency = new();
    private readonly EngineSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureIndex"/> class.
    /// </summary>
    /// <param name="library">The user's library.</param>
    /// <param name="settings">The settings.</param>
    public FeatureIndex(IReadOnlyList<MediaItem> library, EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(library);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        foreach (var item in library.OrderBy(i => i.Id))
        {
            if (_items.ContainsKey(item.Id))
            {
                continue;
            }

            _items[item.Id] = item;
            var features = FeatureExtractor.Extract(item, settings);
            _features[item.Id] = features;
            foreach (var feature in features)
            {
                _documentFrequency[feature] = _documentFrequency.GetValueOrDefault(feature) + 1;
            }
        }
    }

    /// <summary>
    /// Gets the number of items N.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the items, ordered by id.
    /// </summary>
    public IEnumerable<MediaItem> Items => _items.Values.OrderBy(i => i.Id);

    /// <summary>
    /// Gets an item.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The item or <c>null</c>.</returns>
    public MediaItem? Item(Guid id) => _items.GetValueOrDefault(id);

    /// <summary>
    /// Gets the features of an item (empty if unknown).
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The features.</returns>
    public IReadOnlyList<Feature> Features(Guid id) => _features.TryGetValue(id, out var f) ? f : [];

    /// <summary>
    /// Gets idf(f) = ln(N / (1 + n_f)) + 1, or 1 when IDF is disabled.
    /// </summary>
    /// <param name="feature">The feature.</param>
    /// <returns>The IDF.</returns>
    public double Idf(Feature feature)
    {
        if (!_settings.UseIdf || Count == 0)
        {
            return 1;
        }

        var n = _documentFrequency.GetValueOrDefault(feature);
        return Math.Log((double)Count / (1 + n)) + 1;
    }

    /// <summary>
    /// Gets the IDF of a category and raw value.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The IDF.</returns>
    public double Idf(FeatureCategory category, string value) => Idf(Feature.Of(category, value));

    /// <summary>
    /// Builds the weighted feature vector of an item (α_c · idf / sqrt|x_c|), used for similarity.
    /// </summary>
    /// <param name="id">The item id.</param>
    /// <returns>The sparse vector.</returns>
    public Dictionary<string, double> Vector(Guid id)
    {
        var vector = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var group in FeatureExtractor.ByCategory(Features(id)))
        {
            var count = group.Count();
            var alpha = _settings.Weight(group.Key);
            foreach (var feature in group)
            {
                vector[feature.FullKey] = alpha * Idf(feature) / Math.Sqrt(count);
            }
        }

        return vector;
    }
}
