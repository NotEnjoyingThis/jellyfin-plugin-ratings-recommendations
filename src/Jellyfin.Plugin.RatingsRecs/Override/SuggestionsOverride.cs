using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.RatingsRecs.Override;

/// <summary>
/// Rules of the optional Jellyfin 10.11 override (design section 6.6, point 5): which endpoints are touched and when.
/// </summary>
public static class SuggestionsOverride
{
    /// <summary>The hook name on the status page.</summary>
    public const string HookName = "SuggestionsOverride";

    /// <summary>
    /// Finds whether an MVC action is one of the overridden endpoints (names verified on 10.11.11, see docs/NOTES.md).
    /// </summary>
    /// <param name="controller">The controller name (without "Controller").</param>
    /// <param name="action">The action name.</param>
    /// <returns>The target.</returns>
    public static OverrideTarget Match(string? controller, string? action) => (controller, action) switch
    {
        ("Suggestions", "GetSuggestions" or "GetSuggestionsLegacy") => OverrideTarget.Suggestions,
        ("Movies", "GetMovieRecommendations") => OverrideTarget.MovieRecommendations,
        _ => OverrideTarget.None,
    };

    /// <summary>
    /// Reads the <c>type</c> parameter of <c>/Items/Suggestions</c>: only requests for movies and/or series are replaced.
    /// </summary>
    /// <param name="types">The values of the <c>type</c> query parameter (repeated or comma separated).</param>
    /// <param name="kind">"Movie", "Series", or <c>null</c> for both.</param>
    /// <returns><c>true</c> if the response can be replaced.</returns>
    public static bool TryGetKind(IEnumerable<string?> types, out string? kind)
    {
        ArgumentNullException.ThrowIfNull(types);
        kind = null;
        var requested = types
            .SelectMany(t => (t ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (requested.Any(t => t is not ("movie" or "series")))
        {
            return false;
        }

        kind = requested.Count == 1 ? (requested[0] == "movie" ? "Movie" : "Series") : null;
        return true;
    }
}
