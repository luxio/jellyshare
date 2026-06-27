using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.JellyShare.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.JellyShare;

/// <summary>
/// Plugin entry point. Jellyfin discovers this class at startup and loads it.
/// BasePlugin&lt;PluginConfiguration&gt; ties the plugin to its configuration;
/// IHasWebPages makes the configuration page show up in the dashboard.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        // Static reference so other classes can reach the configuration and
        // SaveConfiguration() easily.
        Instance = this;
    }

    /// <summary>Global access to the running plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    public override string Name => "JellyShare";

    // Unique, fixed plugin id. NEVER change it, or it counts as a different plugin.
    public override Guid Id => Guid.Parse("eb5d7894-8eef-4b36-aa6f-5d124e828ce1");

    public override string Description =>
        "Creates public, time-limited share links for videos.";

    /// <summary>Registers the HTML configuration page in the Jellyfin dashboard.</summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            // Path to the embedded resource: namespace + folder + file name.
            EmbeddedResourcePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Configuration.configPage.html",
                GetType().Namespace)
        };
    }
}
