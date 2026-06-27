using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyShare.Models;

/// <summary>A single public share link (stored in shares.json).</summary>
public class ShareInfo
{
    /// <summary>Secret, random token from the URL.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Id of the shared Jellyfin item.</summary>
    public Guid ItemId { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Expiry time. null = never expires.</summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>True when the link has expired (not persisted).</summary>
    [JsonIgnore]
    public bool IsExpired => ExpiresUtc.HasValue && ExpiresUtc.Value < DateTime.UtcNow;
}
