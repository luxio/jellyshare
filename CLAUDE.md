# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A **Jellyfin server plugin** (C# / .NET 9, targeting Jellyfin 10.11) that makes individual videos publicly accessible via secret, time-limited token links, played in an embedded web player — no Jellyfin login required for viewers. It also injects a "share publicly" entry into the web client's item context menu.

## Build

`dotnet` is installed under `~/.dotnet` (not system-wide). Prefix or export PATH first:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet publish Jellyfin.Plugin.JellyShare/Jellyfin.Plugin.JellyShare.csproj -c Release -o ./dist
```

The artifact is `dist/Jellyfin.Plugin.JellyShare.dll`.

## Local test loop (Docker)

There are **no automated tests**; verification is done by running the plugin against a real Jellyfin in Docker. The established loop:

```bash
TEST=<workdir>/jftest   # holds config/ cache/ media/
# 1. drop the freshly built DLL into the mounted plugin dir
cp ./dist/Jellyfin.Plugin.JellyShare.dll "$TEST/config/plugins/JellyShare/"
# 2. reload the plugin
docker restart jellyshare-test
# 3. wait until ready, then exercise endpoints
curl -s http://localhost:8096/System/Info/Public
```

First-time container setup: `docker run -d --name jellyshare-test -p 8096:8096 -v $TEST/config:/config -v $TEST/cache:/cache -v $TEST/media:/media jellyfin/jellyfin:10.11.11` (match the image tag to the Jellyfin version you target). Complete the startup wizard via the `/Startup/*` endpoints, authenticate via `POST /Users/AuthenticateByName` (use header `X-Emby-Authorization`), then call plugin endpoints with `X-Emby-Token`.

Key checks after a change: plugin loads (`docker logs jellyshare-test | grep JellyShare`), and the web-client script is injected (`docker exec jellyshare-test grep JellyShare-Injection /jellyfin/jellyfin-web/index.html`).

## Architecture

**Server side (C#).** `Plugin.cs` is the entry point (`BasePlugin<PluginConfiguration>` + `IHasWebPages`); its `Id` GUID is the plugin's permanent identity and must never change. `ServiceRegistrator` wires up DI: `ShareManager` (singleton) and `WebInjectionService` (hosted service).

- **`ShareManager`** owns all shares and persists them to `PluginConfigurationsPath/JellyShare/shares.json` — deliberately **not** in `PluginConfiguration`, so saving plugin settings can't clobber the share list. Tokens are crypto-random, URL-safe.
- **Two controllers** split by trust boundary:
  - `Api/ShareController` — `[Authorize]`, route `/JellyShare/Shares`. Admin CRUD used by the dashboard config page.
  - `Api/PublicShareController` — `[AllowAnonymous]`, route `/JellyShare`. Serves the player page (`/View/{token}`), the raw file stream (`/Stream/{token}`, with HTTP range support for seeking), and the injected web-client script (`/ClientScript`). **`[AllowAnonymous]` + the secret token is the entire public-access mechanism** — there is no other gate.
- Expiry is enforced on every public hit via `ShareInfo.IsExpired`; expired/unknown tokens return 404.

**Web client integration.** Jellyfin has no official API for adding items to the web UI's context menu, so `WebInjectionService` patches `jellyfin-web/index.html` at server startup (idempotent via a marker comment) to load `/JellyShare/ClientScript`. That script (`ClientScript/jellyshare.js`, an embedded resource) uses a `MutationObserver` to detect opened action sheets and, on a video **detail page** (item id parsed from the URL hash), clones the last menu item and rewrites it into the share entry, then opens custom dialogs that call the admin API.

**Packaging.** `build.yaml` holds `targetAbi` and `guid` for the Jellyfin plugin repo format. `configPage.html` (embedded resource) is the dashboard settings page.

## Gotchas

- **Authorization:** use plain `[Authorize]`. The named policy `"DefaultAuthorization"` does **not** exist in current Jellyfin and throws HTTP 500.
- **Version/ABI must match the server, including the runtime.** Keep `Jellyfin.Controller` version + `TargetFramework` in the `.csproj` and `framework`/`targetAbi` in `build.yaml` aligned with the target Jellyfin release. Jellyfin 10.11 runs on **.NET 9** (`net9.0`); 10.10 and earlier on .NET 8. A wrong TFM fails the build with NU1202; a wrong ABI means the plugin won't load.
- **Material icon glyphs come from a CSS class** (`.material-icons.<name>:before`), not from ligature text. In the client script, swap the icon by changing the class (e.g. `share` → `public`), not by setting `textContent`, or two glyphs render.
- The web files live in the image, not a mounted volume: `docker restart` keeps the injected `index.html`; recreating the container (`docker rm` + `run`) starts fresh and re-injects.
- The GUID appears in `Plugin.cs`, `build.yaml`, and `configPage.html`/`jellyshare.js` (`PLUGIN_ID`) — keep them identical.
