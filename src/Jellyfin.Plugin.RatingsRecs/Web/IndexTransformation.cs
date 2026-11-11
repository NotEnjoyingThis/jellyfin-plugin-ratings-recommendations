using System;
using System.Globalization;

namespace Jellyfin.Plugin.RatingsRecs.Web;

/// <summary>
/// Callback invoked by the File Transformation plugin (through reflection) for jellyfin-web's <c>index.html</c>:
/// adds the plugin stylesheet and script. Must never throw: on any problem the original content is returned.
/// </summary>
public static class IndexTransformation
{
    /// <summary>
    /// Marker used to detect an already transformed page.
    /// </summary>
    public const string Marker = "RatingsRecs/Web/ratings.js";

    /// <summary>
    /// Gets the last time the script was injected into a served index.html (diagnostics for the settings page).
    /// </summary>
    public static DateTime? LastInjectedUtc { get; private set; }

    /// <summary>
    /// Transforms <c>index.html</c>. Signature required by File Transformation: static, one parameter
    /// with a <c>contents</c> property, returns the new content.
    /// </summary>
    /// <param name="payload">The payload with the file content.</param>
    /// <returns>The transformed content.</returns>
    public static string Transform(TransformPayload payload)
    {
        var contents = payload?.Contents ?? string.Empty;
        try
        {
            if (Plugin.Instance?.Configuration.Enabled == false)
            {
                return contents;
            }

            var result = Inject(contents, typeof(IndexTransformation).Assembly.GetName().Version?.ToString() ?? "0");
            if (!ReferenceEquals(result, contents))
            {
                LastInjectedUtc = DateTime.UtcNow;
            }

            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return contents;
        }
    }

    /// <summary>
    /// Inserts the stylesheet before <c>&lt;/head&gt;</c> and the script before <c>&lt;/body&gt;</c>, once.
    /// Relative URLs keep working when Jellyfin runs under a base URL (index.html is served from <c>/web/</c>).
    /// </summary>
    /// <param name="html">The page.</param>
    /// <param name="version">The cache-busting version.</param>
    /// <returns>The page with the tags, or unchanged if already present or not an HTML page.</returns>
    public static string Inject(string html, string version)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (html.Contains(Marker, StringComparison.Ordinal))
        {
            return html;
        }

        var bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyEnd < 0)
        {
            return html;
        }

        var v = Uri.EscapeDataString(version);
        var script = string.Format(CultureInfo.InvariantCulture, "<script defer src=\"../RatingsRecs/Web/ratings.js?v={0}\"></script>", v);
        var result = html.Insert(bodyEnd, script);

        var headEnd = result.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headEnd >= 0)
        {
            var css = string.Format(CultureInfo.InvariantCulture, "<link rel=\"stylesheet\" href=\"../RatingsRecs/Web/ratings.css?v={0}\">", v);
            result = result.Insert(headEnd, css);
        }

        return result;
    }
}
