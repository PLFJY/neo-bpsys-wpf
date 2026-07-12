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
    /// 返回是否已注册插件控件描述符。
    /// </summary>
    bool IsPluginControlRegistered(string fullControlType) => GetPluginDescriptor(fullControlType) is not null;

    /// <summary>
    /// 获取插件控件描述符元数据。
    /// </summary>
    IFrontedPluginControlDescriptor? GetPluginDescriptor(string fullControlType) => null;

    /// <summary>
    /// 获取所有已注册的插件控件描述符。
    /// </summary>
    IReadOnlyCollection<IFrontedPluginControlDescriptor> GetPluginDescriptors() => [];
}
