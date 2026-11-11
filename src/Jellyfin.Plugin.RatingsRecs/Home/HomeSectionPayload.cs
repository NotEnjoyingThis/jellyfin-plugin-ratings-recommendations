using System;

namespace Jellyfin.Plugin.RatingsRecs.Home;

/// <summary>
/// The payload Home Screen Sections passes to a plugin-defined section (<c>{ UserId, AdditionalData }</c>).
/// </summary>
public sealed class HomeSectionPayload
{
    /// <summary>
    /// Gets or sets the user (always the authenticated user, checked by Home Screen Sections).
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the additional data given at registration.
    /// </summary>
    public string? AdditionalData { get; set; }
}
