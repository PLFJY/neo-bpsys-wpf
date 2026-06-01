namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides loaded plugin metadata for fronted layout dependency synchronization.
/// </summary>
public interface IFrontedPluginMetadataProvider
{
    bool IsPluginInstalled(string packageId);

    bool TryGetPluginVersion(string packageId, out string version);

    bool TryGetPluginDisplayName(string packageId, out string displayName);
}
