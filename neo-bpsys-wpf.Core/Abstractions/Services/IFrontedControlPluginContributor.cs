namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Contributes Designer v3 plugin fronted controls during application startup.
/// </summary>
public interface IFrontedControlPluginContributor
{
    /// <summary>
    /// Registers plugin fronted controls.
    /// </summary>
    void RegisterFrontedControls(IFrontedControlPluginRegistry registry);
}
