using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyShare.Services;

/// <summary>
/// Injects a &lt;script&gt; tag into the web interface's index.html at server
/// startup. That way our client script runs in the browser and can extend the
/// context menu.
///
/// Note: this is deliberately "invasive" - it modifies a file of the web
/// interface. After a Jellyfin update the index.html is regenerated; the marker
/// is then missing and we inject again on the next startup.
/// </summary>
public class WebInjectionService : IHostedService
{
    private const string Marker = "<!-- JellyShare-Injection -->";
    private const string Snippet = Marker + "<script defer src=\"/JellyShare/ClientScript\"></script>";

    private readonly IServerApplicationPaths _paths;
    private readonly ILogger<WebInjectionService> _logger;

    public WebInjectionService(IServerApplicationPaths paths, ILogger<WebInjectionService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var indexPath = Path.Combine(_paths.WebPath, "index.html");
            if (!File.Exists(indexPath))
            {
                _logger.LogWarning("JellyShare: index.html not found at {Path}", indexPath);
                return Task.CompletedTask;
            }

            var html = File.ReadAllText(indexPath);
            if (html.Contains(Marker, StringComparison.Ordinal))
            {
                return Task.CompletedTask; // already injected
            }

            var idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                _logger.LogWarning("JellyShare: no </body> found in index.html, injection skipped.");
                return Task.CompletedTask;
            }

            html = html.Insert(idx, Snippet);
            File.WriteAllText(indexPath, html);
            _logger.LogInformation("JellyShare: injected client script into the web interface.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JellyShare: failed to inject into the web interface.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
