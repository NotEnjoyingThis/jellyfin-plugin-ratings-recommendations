using System;

namespace Jellyfin.Plugin.RatingsRecs.Ratings;

/// <summary>
/// Data of the <see cref="IRatingStore.RatingsChanged"/> event.
/// </summary>
public sealed class RatingsChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RatingsChangedEventArgs"/> class.
    /// </summary>
    /// <param name="userId">The user whose ratings changed.</param>
    public RatingsChangedEventArgs(Guid userId)
    {
        UserId = userId;
    }

    /// <summary>
    /// Gets the user whose ratings changed.
    /// </summary>
    public Guid UserId { get; }
}
