using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.JellyShare.Configuration;
using Jellyfin.Plugin.JellyShare.Models;
using Jellyfin.Plugin.JellyShare.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyShare.Api;

/// <summary>
/// Admin API for creating, listing and deleting shares.
/// [Authorize] ensures that ONLY authenticated Jellyfin users may access this -
/// the configuration page calls these endpoints with the admin token.
/// </summary>
[ApiController]
[Authorize]
[Route("JellyShare/Shares")]
public class ShareController : ControllerBase
{
    private readonly ShareManager _shareManager;
    private readonly ILibraryManager _libraryManager;

    public ShareController(ShareManager shareManager, ILibraryManager libraryManager)
    {
        _shareManager = shareManager;
        _libraryManager = libraryManager;
    }

    /// <summary>List of all existing shares.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<ShareDto>> GetShares()
    {
        return Ok(_shareManager.GetAll().Select(ToDto));
    }

    /// <summary>Creates a new share link for the item with the given id.</summary>
    [HttpPost("{itemId}")]
    public ActionResult<ShareDto> CreateShare([FromRoute] Guid itemId, [FromQuery] int? expiryDays)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound("Item not found.");
        }

        var days = expiryDays ?? Plugin.Instance!.Configuration.DefaultExpiryDays;
        var share = _shareManager.CreateShare(itemId, days);
        return Ok(ToDto(share));
    }

    /// <summary>Deletes a share link (the link becomes invalid immediately).</summary>
    [HttpDelete("{token}")]
    public ActionResult DeleteShare([FromRoute] string token)
    {
        return _shareManager.Delete(token) ? NoContent() : NotFound();
    }

    private ShareDto ToDto(ShareInfo s)
    {
        var item = _libraryManager.GetItemById(s.ItemId);
        return new ShareDto
        {
            Token = s.Token,
            ItemId = s.ItemId,
            ItemName = item?.Name ?? "(deleted item)",
            CreatedUtc = s.CreatedUtc,
            ExpiresUtc = s.ExpiresUtc,
            IsExpired = s.IsExpired,
            Url = $"/JellyShare/View/{s.Token}"
        };
    }
}
