using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.RatingsRecs.Configuration;

namespace Jellyfin.Plugin.RatingsRecs.Web;

/// <summary>
/// Builds the web client files served from embedded resources. The script is prefixed with a small
/// public bootstrap object (version, level names chosen by the administrator, IT/EN strings).
/// </summary>
public static class WebAssets
{
    /// <summary>
    /// Languages with a translation file in <c>Web/i18n</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Languages = ["en", "it"];

    private static readonly Assembly Assembly = typeof(WebAssets).Assembly;
    private static readonly string ResourcePrefix = typeof(Plugin).Namespace + ".Web.";

    /// <summary>
    /// Reads an embedded web resource as text.
    /// </summary>
    /// <param name="relativeName">The name under <c>Web/</c> with dots, e.g. <c>i18n.en.json</c>.</param>
    /// <returns>The text, or <c>null</c> if missing.</returns>
    public static string? ReadText(string relativeName)
    {
        using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + relativeName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Builds <c>ratings.js</c>: bootstrap object + client script.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="homeRows">How the home rows are drawn: "hss" (Home Screen Sections), "script" or "off".</param>
    /// <returns>The script.</returns>
    public static string BuildScript(PluginConfiguration config, string homeRows = "script")
    {
        ArgumentNullException.ThrowIfNull(config);
        var strings = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var json = ReadText("i18n." + language + ".json");
            if (json is not null)
            {
                strings[language] = JsonSerializer.Deserialize<JsonElement>(json);
            }
        }

        var bootstrap = new
        {
            version = Assembly.GetName().Version?.ToString() ?? "0",
            enabled = config.Enabled,
            levelNames = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["-3"] = NullIfEmpty(config.NameHate),
                ["-1"] = NullIfEmpty(config.NameWatchable),
                ["1"] = NullIfEmpty(config.NameGood),
                ["3"] = NullIfEmpty(config.NameAmazing),
            },
            strings,
            homeRows,
            homeRowSize = Math.Max(1, config.HomeRowSize),
            similarRow = config.ShowSimilarRow,
            similarRowSize = Math.Max(1, config.SimilarRowSize),
            menuEntry = config.ShowMenuEntry,
        };

        return "window.RatingsRecsBootstrap = " + JsonSerializer.Serialize(bootstrap) + ";\n" + (ReadText("ratings.js") ?? string.Empty);
    }

    /// <summary>
    /// Computes a strong ETag for content.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>The quoted ETag.</returns>
    public static string ETag(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return "\"" + Convert.ToHexString(hash, 0, 12) + "\"";
    }

    /// <summary>
    /// Resolves how the home rows are drawn from the setting and the Home Screen Sections hook.
    /// </summary>
    /// <param name="mode">The setting: Auto, HomeScreenSections, Script or Off.</param>
    /// <param name="homeScreenSectionsActive">Whether the sections are registered in Home Screen Sections.</param>
    /// <returns>"hss", "script" or "off".</returns>
    public static string HomeRows(string? mode, bool homeScreenSectionsActive)
    {
        if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
        {
            return "off";
        }

        if (string.Equals(mode, "Script", StringComparison.OrdinalIgnoreCase))
        {
            return "script";
        }

        // Auto or HomeScreenSections: Home Screen Sections when it works, otherwise the script (degrade, never break).
        return homeScreenSectionsActive ? "hss" : "script";
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
