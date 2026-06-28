# JellyShare

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

A Jellyfin plugin that makes individual videos publicly available through secret
**token links with an expiry date** — viewers do not need to log in to Jellyfin.
The video plays in its own page with an embedded web player. The plugin also adds
a **"Share publicly"** entry to the item context menu in the web interface.

## How it works

- **Admin API** (`/JellyShare/Shares`, login required): create / list / delete shares.
- **Public page** (`/JellyShare/View/{token}`, no login): HTML with a `<video>` player.
- **Stream** (`/JellyShare/Stream/{token}`, no login): serves the video file directly
  (direct play, with seek support).
- **Client script** (`/JellyShare/ClientScript`): injected into the web interface to add
  the context-menu entry.
- **Configuration page** in the dashboard under *Plugins → JellyShare*.

Shares are stored in `<data-folder>/plugins/configurations/JellyShare/shares.json`.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download) (Jellyfin 10.11 targets .NET 9)
- Jellyfin server 10.11.x (for other versions, adjust the `TargetFramework` and
  `Jellyfin.Controller` `Version` in the `.csproj`, and `framework`/`targetAbi` in
  `build.yaml` — note Jellyfin 10.10 and earlier use .NET 8)

## Build

```bash
dotnet publish Jellyfin.Plugin.JellyShare/Jellyfin.Plugin.JellyShare.csproj \
  -c Release -o ./dist
```

The resulting file is `dist/Jellyfin.Plugin.JellyShare.dll`.

## Install (manual)

1. In the Jellyfin data folder, create the folder `plugins/JellyShare/`.
   (Typical paths: Linux `/var/lib/jellyfin/plugins`, Docker `/config/plugins`,
   macOS `~/.local/share/jellyfin/plugins`.)
2. Copy `Jellyfin.Plugin.JellyShare.dll` into it.
3. Restart the Jellyfin server.
4. Dashboard → Plugins → **JellyShare** should appear.

## Usage

1. Open a video and choose **"Share publicly"** from the item context menu (⋯).
   Enter the validity in days, then the public link is created and copied to your
   clipboard.
2. Alternatively, from the dashboard: Plugins → JellyShare → enter the item id
   (from the `id=` parameter in the address bar), optionally set the expiry days,
   then **Create share link**. Use **Copy link** in the table to copy a link.

## Known limitations (possible enhancements)

- **Direct play, no transcoding:** in the browser, only compatible formats play
  (e.g. MP4/H.264/AAC, WebM). MKV/HEVC may not play — for that, Jellyfin's
  transcoding/HLS pipeline would need to be wired in later.
- Expired shares are rejected but not automatically removed from the file.
- The context-menu entry currently appears on the item **detail page** only, not on
  small cards in lists.

## License

This project is licensed under the **GNU General Public License v3.0 or later**
(`GPL-3.0-or-later`) — see the [LICENSE](LICENSE) file for the full text.

Jellyfin itself is GPL-licensed and this plugin links against its interfaces, so
GPL keeps the plugin aligned with the Jellyfin ecosystem.
