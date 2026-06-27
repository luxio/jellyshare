using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyShare.Configuration;

/// <summary>
/// Persistent plugin settings. Jellyfin serializes this class to XML
/// automatically and loads it again on startup.
/// The actual share entries are NOT stored here but in a separate JSON file
/// (see ShareManager) - this prevents saving the settings from accidentally
/// overwriting the shares.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Default validity of new links in days. 0 = never expires.</summary>
    public int DefaultExpiryDays { get; set; } = 7;
}
