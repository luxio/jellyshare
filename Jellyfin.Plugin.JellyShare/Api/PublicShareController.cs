using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Jellyfin.Plugin.JellyShare.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyShare.Api;

/// <summary>
/// Public endpoints. [AllowAnonymous] lifts Jellyfin's usual login requirement -
/// anyone who knows the secret token reaches the player and stream without a login.
/// </summary>
[ApiController]
[Route("JellyShare")]
public class PublicShareController : ControllerBase
{
    private readonly ShareManager _shareManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<PublicShareController> _logger;

    public PublicShareController(
        ShareManager shareManager,
        ILibraryManager libraryManager,
        ILogger<PublicShareController> logger)
    {
        _shareManager = shareManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>HTML page with an embedded video player.</summary>
    [AllowAnonymous]
    [HttpGet("View/{token}")]
    public ActionResult GetViewPage([FromRoute] string token)
    {
        var share = _shareManager.GetByToken(token);
        if (share is null || share.IsExpired)
        {
            return NotFound("This link is invalid or has expired.");
        }

        var item = _libraryManager.GetItemById(share.ItemId);
        if (item is null)
        {
            return NotFound();
        }

        // Keep the secret token out of Referer headers on any outbound navigation.
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Content(BuildHtml(token, item.Name), "text/html");
    }

    /// <summary>Masks a token for logging so the secret never lands in the logs.</summary>
    private static string Mask(string token) =>
        string.IsNullOrEmpty(token) ? "(empty)"
        : token.Length <= 6 ? "******"
        : token[..6] + "...";

    /// <summary>
    /// Delivers the actual video stream (direct play of the original file) with
    /// range support, so that seeking works in the player.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("Stream/{token}")]
    public ActionResult GetStream([FromRoute] string token)
    {
        var share = _shareManager.GetByToken(token);
        if (share is null || share.IsExpired)
        {
            _logger.LogWarning("JellyShare: stream rejected, share missing or expired for token {Token}", Mask(token));
            return NotFound();
        }

        var item = _libraryManager.GetItemById(share.ItemId);
        if (item is null)
        {
            _logger.LogWarning("JellyShare: stream item {ItemId} not found", share.ItemId);
            return NotFound();
        }

        foreach (var candidate in GetCandidatePaths(item))
        {
            var resolved = ResolveRealPath(candidate);
            if (resolved is null)
            {
                continue;
            }

            try
            {
                var stream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
                return File(stream, GetContentType(resolved), enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JellyShare: could not open {Path} for item {ItemId}", resolved, item.Id);
            }
        }

        _logger.LogWarning(
            "JellyShare: no openable file for item {ItemId} (type {Type}). item.Path={ItemPath}",
            item.Id,
            item.GetType().Name,
            item.Path);
        return NotFound();
    }

    /// <summary>
    /// Candidate file paths for an item: item.Path plus the media-source paths
    /// (with path substitution applied, as Jellyfin does for playback).
    /// </summary>
    private List<string> GetCandidatePaths(MediaBrowser.Controller.Entities.BaseItem item)
    {
        var paths = new List<string>();
        void Add(string? p)
        {
            if (!string.IsNullOrEmpty(p) && !paths.Contains(p))
            {
                paths.Add(p);
            }
        }

        Add(item.Path);
        try
        {
            foreach (var source in item.GetMediaSources(true))
            {
                Add(source.Path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JellyShare: GetMediaSources failed for item {ItemId}", item.Id);
        }

        return paths;
    }

    /// <summary>
    /// Resolves a stored path to a real, existing path on disk. Walks the path
    /// segment by segment and, when an exact match is missing, scans the parent
    /// directory and matches entries with Unicode normalization. This handles
    /// NFC/NFD filename mismatches (common when files were created on macOS and
    /// served from a NAS), where File.Exists with the stored string fails even
    /// though the file is present.
    /// </summary>
    private string? ResolveRealPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (System.IO.File.Exists(path))
        {
            return path;
        }

        // Only absolute POSIX paths can be walked from root this way.
        if (!path.StartsWith('/'))
        {
            return null;
        }

        var current = "/";
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = current + (current.EndsWith('/') ? string.Empty : "/") + segment;
            if (Directory.Exists(next) || System.IO.File.Exists(next))
            {
                current = next;
                continue;
            }

            string? match = null;
            try
            {
                var target = segment.Normalize(System.Text.NormalizationForm.FormC);
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    var name = Path.GetFileName(entry).Normalize(System.Text.NormalizationForm.FormC);
                    if (string.Equals(name, target, StringComparison.Ordinal))
                    {
                        match = entry;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JellyShare: directory scan failed in {Dir}", current);
                return null;
            }

            if (match is null)
            {
                return null;
            }

            current = match;
        }

        return System.IO.File.Exists(current) ? current : null;
    }

    /// <summary>Serves the client script that is injected into the web interface.</summary>
    [AllowAnonymous]
    [HttpGet("ClientScript")]
    public ActionResult GetClientScript()
    {
        var assembly = GetType().Assembly;
        var resourceName = $"{typeof(Plugin).Namespace}.ClientScript.jellyshare.js";
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, "application/javascript; charset=utf-8");
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".webm" => "video/webm",
            ".ogv" => "video/ogg",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            _ => "video/mp4"
        };

    private static string BuildHtml(string token, string title)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var streamUrl = $"/JellyShare/Stream/{Uri.EscapeDataString(token)}";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>{safeTitle}</title>
  <style>
    body {{ margin:0; background:#101010; color:#eee; font-family:system-ui,sans-serif; }}
    header {{ padding:16px 20px; font-size:18px; font-weight:600; }}
    .wrap {{ max-width:1100px; margin:0 auto; padding:0 16px 32px; }}
    video {{ width:100%; border-radius:8px; background:#000; }}
  </style>
</head>
<body>
  <header>{safeTitle}</header>
  <div class=""wrap"">
    <video controls autoplay playsinline preload=""metadata"">
      <source src=""{streamUrl}"">
      Your browser cannot play this video.
    </video>
  </div>
</body>
</html>";
    }
}
