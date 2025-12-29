using neo_bpsys_wpf.Core.Abstractions.Plugins;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 插件导航服务接口 - 管理插件页面的导航注册
/// Plugin navigation service interface - Manages plugin page navigation registration
/// </summary>
public interface IPluginNavigationService
{
    /// <summary>
    /// 注册插件页面到导航系统
    /// Register plugin pages to navigation system
    /// </summary>
    /// <param name="plugin">UI插件 UI plugin</param>
    void RegisterPluginPages(IUIPlugin plugin);

    /// <summary>
    /// 注销插件页面
    /// Unregister plugin pages
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    void UnregisterPluginPages(string pluginId);

    /// <summary>
    /// 获取所有插件页面描述符
    /// Get all plugin page descriptors
    /// </summary>
    IEnumerable<PluginPageDescriptor> GetPluginPages();

    /// <summary>
    /// 插件页面已注册事件
    /// Plugin pages registered event
    /// </summary>
    event EventHandler<PluginPagesRegisteredEventArgs>? PluginPagesRegistered;

    /// <summary>
    /// 插件页面已注销事件
    /// Plugin pages unregistered event
    /// </summary>
    event EventHandler<PluginPagesUnregisteredEventArgs>? PluginPagesUnregistered;
}

/// <summary>
/// 插件页面已注册事件参数
/// Plugin pages registered event args
/// </summary>
public class PluginPagesRegisteredEventArgs : EventArgs
{
    /// <summary>
    /// 插件ID
    /// Plugin ID
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// 页面描述符列表
    /// Page descriptors
    /// </summary>
    public required IEnumerable<PluginPageDescriptor> Pages { get; init; }
}

/// <summary>
/// 插件页面已注销事件参数
/// Plugin pages unregistered event args
/// </summary>
public class PluginPagesUnregisteredEventArgs : EventArgs
{
    /// <summary>
    /// 插件ID
    /// Plugin ID
    /// </summary>
    public required string PluginId { get; init; }
}
