namespace Jellyfin.Plugin.RatingsRecs.Web;

/// <summary>
/// Payload passed by File Transformation to the callback (built from <c>{ "contents": "..." }</c>).
/// </summary>
public sealed class TransformPayload
{
    /// <summary>
    /// Gets or sets the file content.
    /// </summary>
    public string? Contents { get; set; }
}
