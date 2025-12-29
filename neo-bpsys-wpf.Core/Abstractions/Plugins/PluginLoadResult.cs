namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件加载结果
/// Plugin load result
/// </summary>
public class PluginLoadResult
{
    /// <summary>
    /// 是否加载成功
    /// Whether loading was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 插件元数据
    /// Plugin metadata
    /// </summary>
    public PluginMetadata? Metadata { get; init; }

    /// <summary>
    /// 插件实例
    /// Plugin instance
    /// </summary>
    public IPlugin? Plugin { get; init; }

    /// <summary>
    /// 错误消息
    /// Error message
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 异常信息
    /// Exception
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 创建成功结果
    /// Create success result
    /// </summary>
    public static PluginLoadResult CreateSuccess(PluginMetadata metadata, IPlugin plugin)
    {
        return new PluginLoadResult
        {
            Success = true,
            Metadata = metadata,
            Plugin = plugin
        };
    }

    /// <summary>
    /// 创建失败结果
    /// Create failure result
    /// </summary>
    public static PluginLoadResult CreateFailure(string errorMessage, Exception? exception = null)
    {
        return new PluginLoadResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}
