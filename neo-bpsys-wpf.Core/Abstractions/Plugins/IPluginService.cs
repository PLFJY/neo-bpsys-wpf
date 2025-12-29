namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件服务接口 - 管理插件的生命周期
/// Plugin service interface - Manages plugin lifecycle
/// </summary>
public interface IPluginService
{
    /// <summary>
    /// 所有已加载的插件
    /// All loaded plugins
    /// </summary>
    IReadOnlyList<PluginMetadata> LoadedPlugins { get; }

    /// <summary>
    /// 发现并扫描插件目录
    /// Discover and scan plugin directory
    /// </summary>
    /// <param name="pluginDirectory">插件目录 Plugin directory</param>
    /// <returns>发现的插件数量 Number of discovered plugins</returns>
    Task<int> DiscoverPluginsAsync(string pluginDirectory);

    /// <summary>
    /// 加载指定插件
    /// Load specific plugin
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    /// <returns>加载结果 Load result</returns>
    Task<PluginLoadResult> LoadPluginAsync(string pluginId);

    /// <summary>
    /// 卸载指定插件
    /// Unload specific plugin
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    Task<bool> UnloadPluginAsync(string pluginId);

    /// <summary>
    /// 启动指定插件
    /// Start specific plugin
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    Task<bool> StartPluginAsync(string pluginId);

    /// <summary>
    /// 停止指定插件
    /// Stop specific plugin
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    Task<bool> StopPluginAsync(string pluginId);

    /// <summary>
    /// 启用指定插件
    /// Enable specific plugin
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    Task<bool> EnablePluginAsync(string pluginId);

    /// <summary>
    /// 禁用指定插件
    /// Disable specific plugin
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    Task<bool> DisablePluginAsync(string pluginId);

    /// <summary>
    /// 获取插件实例
    /// Get plugin instance
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    /// <returns>插件实例 Plugin instance</returns>
    IPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// 获取插件元数据
    /// Get plugin metadata
    /// </summary>
    /// <param name="pluginId">插件ID Plugin ID</param>
    /// <returns>插件元数据 Plugin metadata</returns>
    PluginMetadata? GetPluginMetadata(string pluginId);

    /// <summary>
    /// 获取所有UI插件
    /// Get all UI plugins
    /// </summary>
    /// <returns>UI插件列表 UI plugin list</returns>
    IEnumerable<IUIPlugin> GetUIPlugins();

    /// <summary>
    /// 插件状态改变事件
    /// Plugin state changed event
    /// </summary>
    event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;
}
