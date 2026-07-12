using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 为服务和设计器 v3 提供内置和插件 WPF 前台窗口描述符。
/// </summary>
public interface IFrontedWindowRegistry
{
    /// <summary>
    /// 获取所有已接受的描述符，包括内置窗口、插件 XAML 窗口和插件布局窗口。
    /// </summary>
    IReadOnlyList<IFrontedWindowDescriptor> GetWindows();

    /// <summary>
    /// 获取其布局可由设计器 v3 管理的窗口。
    /// </summary>
    IReadOnlyList<IFrontedWindowDescriptor> GetCustomizableLayoutWindows();

    /// <summary>
    /// 获取在前台管理页可见的窗口，使用稳定的回退分组和排序。
    /// </summary>
    /// <returns>可管理的窗口描述符。</returns>
    IReadOnlyList<IFrontedWindowDescriptor> GetManageableWindows();

    /// <summary>
    /// 按稳定的运行时 <see cref="IFrontedWindowDescriptor.WindowId"/> 查找描述符。
    /// </summary>
    bool TryGetByWindowId(string windowId, out IFrontedWindowDescriptor descriptor);

    /// <summary>
    /// 按布局/包标识查找描述符，包括插件标识，例如
    /// <c>plugin:top.plfjy.example/Overlay</c>。
    /// </summary>
    bool TryGetByFullWindowType(string fullWindowType, out IFrontedWindowDescriptor descriptor);

    /// <summary>
    /// 获取已接受的插件窗口描述符。
    /// </summary>
    IReadOnlyList<FrontedPluginWindowDescriptor> GetPluginWindows();

    /// <summary>
    /// 获取内置前台窗口描述符。
    /// </summary>
    IReadOnlyList<FrontedBuiltInWindowDescriptor> GetBuiltInWindows();
}
