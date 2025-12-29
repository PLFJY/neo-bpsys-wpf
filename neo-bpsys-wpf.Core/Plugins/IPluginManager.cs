namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件管理服务接口
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// 获取所有已加载的插件
    /// </summary>
    IReadOnlyList<IPlugin> LoadedPlugins { get; }

    /// <summary>
    /// 获取所有插件的元数据
    /// </summary>
    IReadOnlyList<PluginMetadata> AllPluginMetadata { get; }

    /// <summary>
    /// 发现指定目录中的插件
    /// </summary>
    /// <param name="pluginDirectory">插件目录</param>
    /// <returns>发现的插件元数据列表</returns>
    Task<IReadOnlyList<PluginMetadata>> DiscoverPluginsAsync(string pluginDirectory);

    /// <summary>
    /// 加载插件
    /// </summary>
    /// <param name="pluginPath">插件程序集路径</param>
    /// <returns>加载结果</returns>
    Task<PluginLoadResult> LoadPluginAsync(string pluginPath);

    /// <summary>
    /// 加载指定目录中的所有插件
    /// </summary>
    /// <param name="pluginDirectory">插件目录</param>
    /// <returns>加载结果列表</returns>
    Task<IReadOnlyList<PluginLoadResult>> LoadPluginsAsync(string pluginDirectory);

    /// <summary>
    /// 卸载插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否成功卸载</returns>
    Task<bool> UnloadPluginAsync(string pluginId);

    /// <summary>
    /// 启用插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否成功启用</returns>
    Task<bool> EnablePluginAsync(string pluginId);

    /// <summary>
    /// 禁用插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否成功禁用</returns>
    Task<bool> DisablePluginAsync(string pluginId);

    /// <summary>
    /// 获取插件实例
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件实例</returns>
    IPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// 获取插件元数据
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件元数据</returns>
    PluginMetadata? GetPluginMetadata(string pluginId);

    /// <summary>
    /// 检查插件是否已加载
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否已加载</returns>
    bool IsPluginLoaded(string pluginId);

    /// <summary>
    /// 检查插件是否已启用
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否已启用</returns>
    bool IsPluginEnabled(string pluginId);

    /// <summary>
    /// 重新加载插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>加载结果</returns>
    Task<PluginLoadResult> ReloadPluginAsync(string pluginId);

    /// <summary>
    /// 获取插件的依赖关系
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>依赖的插件ID列表</returns>
    IReadOnlyList<string> GetPluginDependencies(string pluginId);

    /// <summary>
    /// 获取依赖指定插件的其他插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>依赖此插件的插件ID列表</returns>
    IReadOnlyList<string> GetDependentPlugins(string pluginId);

    /// <summary>
    /// 当插件加载时触发的事件
    /// </summary>
    event EventHandler<PluginLoadedEventArgs>? PluginLoaded;

    /// <summary>
    /// 当插件卸载时触发的事件
    /// </summary>
    event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;

    /// <summary>
    /// 当插件启用时触发的事件
    /// </summary>
    event EventHandler<PluginStateChangedEventArgs>? PluginEnabled;

    /// <summary>
    /// 当插件禁用时触发的事件
    /// </summary>
    event EventHandler<PluginStateChangedEventArgs>? PluginDisabled;

    /// <summary>
    /// 当插件加载失败时触发的事件
    /// </summary>
    event EventHandler<PluginLoadFailedEventArgs>? PluginLoadFailed;
}

/// <summary>
/// 插件加载结果
/// </summary>
public sealed class PluginLoadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 插件元数据
    /// </summary>
    public PluginMetadata? Metadata { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 异常（如果有）
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static PluginLoadResult Succeeded(PluginMetadata metadata) => new()
    {
        Success = true,
        Metadata = metadata
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static PluginLoadResult Failed(string errorMessage, Exception? exception = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        Exception = exception
    };
}

/// <summary>
/// 插件加载事件参数
/// </summary>
public sealed class PluginLoadedEventArgs : EventArgs
{
    /// <summary>
    /// 插件实例
    /// </summary>
    public required IPlugin Plugin { get; init; }

    /// <summary>
    /// 插件元数据
    /// </summary>
    public required PluginMetadata Metadata { get; init; }
}

/// <summary>
/// 插件卸载事件参数
/// </summary>
public sealed class PluginUnloadedEventArgs : EventArgs
{
    /// <summary>
    /// 插件ID
    /// </summary>
    public required string PluginId { get; init; }
}

/// <summary>
/// 插件状态变更事件参数
/// </summary>
public sealed class PluginStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 插件ID
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// 插件元数据
    /// </summary>
    public required PluginMetadata Metadata { get; init; }
}

/// <summary>
/// 插件加载失败事件参数
/// </summary>
public sealed class PluginLoadFailedEventArgs : EventArgs
{
    /// <summary>
    /// 插件路径
    /// </summary>
    public required string PluginPath { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// 异常
    /// </summary>
    public Exception? Exception { get; init; }
}
