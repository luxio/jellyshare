using System;

namespace Jellyfin.Plugin.JellyShare.Models;

/// <summary>What the admin API returns (including a ready-made URL and item name).</summary>
public class ShareDto
{
    public string Token { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public bool IsExpired { get; set; }

    /// <summary>Relative path to the public player page.</summary>
    public string Url { get; set; } = string.Empty;
}
