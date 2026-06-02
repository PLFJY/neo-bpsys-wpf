using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Implemented by plugins that contribute v3 fronted windows.
/// </summary>
public interface IFrontedWindowPluginContributor
{
    /// <summary>
    /// Returns plugin fronted window descriptors. The host validates these during startup before the registry is built.
    /// </summary>
    IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows();
}
