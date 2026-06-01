using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件工厂注册表。
/// </summary>
public interface IFrontedControlRegistry
{
    /// <summary>
    /// 按控件类型获取控件工厂。
    /// </summary>
    IFrontedControl? GetControl(string controlType);

    /// <summary>
    /// 获取所有控件工厂。
    /// </summary>
    IReadOnlyCollection<IFrontedControl> GetControls();

    /// <summary>
    /// Returns whether a plugin control descriptor has been registered.
    /// </summary>
    bool IsPluginControlRegistered(string fullControlType) => GetPluginDescriptor(fullControlType) is not null;

    /// <summary>
    /// Gets plugin control descriptor metadata.
    /// </summary>
    IFrontedPluginControlDescriptor? GetPluginDescriptor(string fullControlType) => null;

    /// <summary>
    /// Gets all registered plugin control descriptors.
    /// </summary>
    IReadOnlyCollection<IFrontedPluginControlDescriptor> GetPluginDescriptors() => [];
}
