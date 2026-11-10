namespace Jellyfin.Plugin.RatingsRecs.Hooks;

/// <summary>
/// State of an integration hook (web injection, Home Screen Sections, Jellyfin 12 provider, 10.11 override).
/// </summary>
public enum HookState
{
    /// <summary>Not started yet.</summary>
    Pending,

    /// <summary>Working.</summary>
    Active,

    /// <summary>Turned off in the settings.</summary>
    Disabled,

    /// <summary>A required companion plugin or API is missing; the hook stays off.</summary>
    Unavailable,

    /// <summary>The hook failed and switched itself off.</summary>
    Failed,
}
