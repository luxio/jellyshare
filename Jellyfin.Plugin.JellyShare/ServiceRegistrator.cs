using Jellyfin.Plugin.JellyShare.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JellyShare;

/// <summary>
/// Jellyfin calls this class automatically at startup and lets us register our
/// own services in the dependency injection container. That way the controllers
/// can simply request the ShareManager in their constructor.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ShareManager>();

        // Runs at server startup and injects our client script into the web interface.
        serviceCollection.AddHostedService<WebInjectionService>();
    }
}
