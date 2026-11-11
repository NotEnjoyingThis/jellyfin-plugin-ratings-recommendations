using System;
using System.Globalization;

namespace Jellyfin.Plugin.RatingsRecs.Hooks;

/// <summary>
/// Startup check of the plugin build against the running server. Jellyfin loads a plugin on any server at least as
/// new as its <c>targetAbi</c>, so the 10.11 build could be loaded by Jellyfin 12 (e.g. a zip installed by hand).
/// </summary>
public static class Compatibility
{
    /// <summary>The hook name on the status page.</summary>
    public const string HookName = "Compatibility";

    /// <summary>
    /// Checks a build line ("10.11" or "12") against the server version.
    /// </summary>
    /// <param name="buildLine">The Jellyfin line the plugin was built for.</param>
    /// <param name="server">The server version.</param>
    /// <returns>Active when they match, Failed otherwise, with a message for the settings page.</returns>
    public static (HookState State, string Message) Check(string buildLine, Version server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var serverText = server.ToString(3);
        var ok = buildLine switch
        {
            "10.11" => server.Major == 10 && server.Minor == 11 && server.Build >= 9,
            "12" => server.Major >= 12,
            _ => false,
        };
        if (ok)
        {
            return (HookState.Active, string.Format(CultureInfo.InvariantCulture, "Build for Jellyfin {0} on Jellyfin {1}.", buildLine, serverText));
        }

        var expected = server.Major >= 12 ? "jf12" : "jf10.11";
        var message = server.Major == 10 && server.Minor == 11 && server.Build < 9
            ? string.Format(CultureInfo.InvariantCulture, "Jellyfin {0} is too old: the plugin needs 10.11.9 or later.", serverText)
            : string.Format(CultureInfo.InvariantCulture, "This is the build for Jellyfin {0} but the server runs {1}: install the {2} zip from the catalog.", buildLine, serverText, expected);
        return (HookState.Failed, message);
    }
}
