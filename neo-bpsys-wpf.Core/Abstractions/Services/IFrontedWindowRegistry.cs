using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides built-in and plugin WPF fronted window descriptors to services and Designer v3.
/// </summary>
public interface IFrontedWindowRegistry
{
    /// <summary>
    /// Gets all accepted descriptors, including built-in, plugin XAML, and plugin layout windows.
    /// </summary>
    IReadOnlyList<IFrontedWindowDescriptor> GetWindows();

    /// <summary>
    /// Gets windows whose layouts can be managed by Designer v3.
    /// </summary>
    IReadOnlyList<IFrontedWindowDescriptor> GetCustomizableLayoutWindows();

    /// <summary>
    /// Gets windows visible in the frontend management page, with stable fallback grouping and ordering.
    /// </summary>
    /// <returns>The manageable window descriptors.</returns>
    IReadOnlyList<IFrontedWindowDescriptor> GetManageableWindows();

    /// <summary>
    /// Looks up a descriptor by stable runtime <see cref="IFrontedWindowDescriptor.WindowId"/>.
    /// </summary>
    bool TryGetByWindowId(string windowId, out IFrontedWindowDescriptor descriptor);

    /// <summary>
    /// Looks up a descriptor by layout/package identity, including plugin identities such as
    /// <c>plugin:top.plfjy.example/Overlay</c>.
    /// </summary>
    bool TryGetByFullWindowType(string fullWindowType, out IFrontedWindowDescriptor descriptor);

    /// <summary>
    /// Gets accepted plugin window descriptors.
    /// </summary>
    IReadOnlyList<FrontedPluginWindowDescriptor> GetPluginWindows();

    /// <summary>
    /// Gets built-in fronted window descriptors.
    /// </summary>
    IReadOnlyList<FrontedBuiltInWindowDescriptor> GetBuiltInWindows();
}
