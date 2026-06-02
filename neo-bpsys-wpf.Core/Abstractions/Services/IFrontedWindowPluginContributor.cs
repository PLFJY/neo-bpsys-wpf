using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Implemented by plugins that contribute v3 fronted windows.
/// </summary>
public interface IFrontedWindowPluginContributor
{
    IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows();
}
