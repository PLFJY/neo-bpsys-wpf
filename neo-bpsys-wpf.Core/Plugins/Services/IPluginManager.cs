using neo_bpsys_wpf.Core.Plugins.Abstractions;

namespace neo_bpsys_wpf.Core.Plugins.Services;

/// <summary>
/// 插件管理器接口
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// 获取所有已加载的插件
    /// </summary>
    IReadOnlyList<IPlugin> LoadedPlugins { get; }

    /// <summary>
    /// 获取指定ID的插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件实例，如果不存在则返回null</returns>
    IPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// 检查插件是否已加载
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否已加载</returns>
    bool IsPluginLoaded(string pluginId);

    /// <summary>
    /// 加载指定目录的插件
    /// </summary>
    /// <param name="pluginPath">插件目录路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>加载结果</returns>
    Task<PluginLoadResult> LoadPluginAsync(string pluginPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 卸载插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 禁用插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重新加载插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取插件状态
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件状态</returns>
    PluginState GetPluginState(string pluginId);
}

/// <summary>
/// 插件加载结果
/// </summary>
public class PluginLoadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 加载的插件（成功时）
    /// </summary>
    public IPlugin? Plugin { get; init; }

    /// <summary>
    /// 错误信息（失败时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 异常（失败时）
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static PluginLoadResult Succeeded(IPlugin plugin) => new()
    {
        Success = true,
        Plugin = plugin
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static PluginLoadResult Failed(string message, Exception? exception = null) => new()
    {
        Success = false,
        ErrorMessage = message,
        Exception = exception
    };
}
