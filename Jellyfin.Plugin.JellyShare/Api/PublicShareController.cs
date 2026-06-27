using System;
using System.IO;
using System.Net;
using Jellyfin.Plugin.JellyShare.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public PublicShareController(ShareManager shareManager, ILibraryManager libraryManager)
    {
        _shareManager = shareManager;
        _libraryManager = libraryManager;
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

        return Content(BuildHtml(token, item.Name), "text/html");
    }

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
            return NotFound();
        }

        var item = _libraryManager.GetItemById(share.ItemId);
        if (item is null || string.IsNullOrEmpty(item.Path) || !System.IO.File.Exists(item.Path))
        {
            return NotFound();
        }

        var stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, GetContentType(item.Path), enableRangeProcessing: true);
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
