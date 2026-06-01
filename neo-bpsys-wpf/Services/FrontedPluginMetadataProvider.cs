using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services;

public sealed class FrontedPluginMetadataProvider : IFrontedPluginMetadataProvider
{
    public bool IsPluginInstalled(string packageId)
    {
        return IPluginService.LoadedPlugins.Any(plugin =>
            string.Equals(plugin.Manifest.Id, packageId, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryGetPluginVersion(string packageId, out string version)
    {
        var plugin = IPluginService.LoadedPlugins.FirstOrDefault(plugin =>
            string.Equals(plugin.Manifest.Id, packageId, StringComparison.OrdinalIgnoreCase));
        version = plugin?.Manifest.Version ?? string.Empty;
        return !string.IsNullOrWhiteSpace(version);
    }

    public bool TryGetPluginDisplayName(string packageId, out string displayName)
    {
        var plugin = IPluginService.LoadedPlugins.FirstOrDefault(plugin =>
            string.Equals(plugin.Manifest.Id, packageId, StringComparison.OrdinalIgnoreCase));
        displayName = plugin?.Manifest.Name ?? string.Empty;
        return !string.IsNullOrWhiteSpace(displayName);
    }
}
