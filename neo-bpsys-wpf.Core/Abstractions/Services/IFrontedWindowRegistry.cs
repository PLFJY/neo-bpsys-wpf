using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides built-in and plugin fronted window descriptors.
/// </summary>
public interface IFrontedWindowRegistry
{
    IReadOnlyList<IFrontedWindowDescriptor> GetWindows();

    IReadOnlyList<IFrontedWindowDescriptor> GetCustomizableLayoutWindows();

    bool TryGetByWindowId(string windowId, out IFrontedWindowDescriptor descriptor);

    bool TryGetByFullWindowType(string fullWindowType, out IFrontedWindowDescriptor descriptor);

    IReadOnlyList<FrontedPluginWindowDescriptor> GetPluginWindows();

    IReadOnlyList<FrontedBuiltInWindowDescriptor> GetBuiltInWindows();
}
