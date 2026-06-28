using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.JellyShare.Models;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.JellyShare.Services;

/// <summary>
/// Manages all share links and stores them in a JSON file in the plugin data
/// folder. Created via dependency injection as a singleton (see
/// ServiceRegistrator), so it exists exactly once in the server.
/// </summary>
public class ShareManager
{
    private readonly string _storePath;
    private readonly object _lock = new();
    private readonly List<ShareInfo> _shares;

    public ShareManager(IApplicationPaths applicationPaths)
    {
        var dir = Path.Combine(applicationPaths.PluginConfigurationsPath, "JellyShare");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "shares.json");
        _shares = Load();
    }

    public IReadOnlyList<ShareInfo> GetAll()
    {
        lock (_lock)
        {
            return _shares.ToList();
        }
    }

    public ShareInfo CreateShare(Guid itemId, int expiryDays)
    {
        var share = new ShareInfo
        {
            Token = GenerateToken(),
            ItemId = itemId,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = expiryDays > 0 ? DateTime.UtcNow.AddDays(expiryDays) : null
        };

        lock (_lock)
        {
            _shares.Add(share);
            Save();
        }

        return share;
    }

    public ShareInfo? GetByToken(string token)
    {
        lock (_lock)
        {
            return _shares.FirstOrDefault(s => TokensEqual(s.Token, token));
        }
    }

    public bool Delete(string token)
    {
        lock (_lock)
        {
            var removed = _shares.RemoveAll(s => TokensEqual(s.Token, token)) > 0;
            if (removed)
            {
                Save();
            }

            return removed;
        }
    }

    private List<ShareInfo> Load()
    {
        if (!File.Exists(_storePath))
        {
            return new List<ShareInfo>();
        }

        try
        {
            var json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<List<ShareInfo>>(json) ?? new List<ShareInfo>();
        }
        catch
        {
            // Don't let a corrupted file crash the plugin.
            return new List<ShareInfo>();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_shares, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storePath, json);
    }

    /// <summary>Constant-time token comparison to avoid timing side channels.</summary>
    private static bool TokensEqual(string a, string b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        // FixedTimeEquals is constant-time for equal-length inputs and returns
        // false for differing lengths (token length is fixed, so this is fine).
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }

    /// <summary>Cryptographically random, URL-safe token.</summary>
    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
