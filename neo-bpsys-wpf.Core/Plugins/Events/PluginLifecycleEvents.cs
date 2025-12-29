using neo_bpsys_wpf.Core.Plugins.Abstractions;

namespace neo_bpsys_wpf.Core.Plugins.Events;

/// <summary>
/// 插件生命周期事件基类
/// </summary>
public abstract class PluginLifecycleEvent : PluginEventBase
{
    /// <summary>
    /// 相关插件
    /// </summary>
    public required IPluginMetadata PluginMetadata { get; init; }
}

/// <summary>
/// 插件加载事件
/// </summary>
public sealed class PluginLoadedEvent : PluginLifecycleEvent;

/// <summary>
/// 插件初始化事件
/// </summary>
public sealed class PluginInitializedEvent : PluginLifecycleEvent;

/// <summary>
/// 插件启动事件
/// </summary>
public sealed class PluginStartedEvent : PluginLifecycleEvent;

/// <summary>
/// 插件停止事件
/// </summary>
public sealed class PluginStoppedEvent : PluginLifecycleEvent;

/// <summary>
/// 插件卸载事件
/// </summary>
public sealed class PluginUnloadedEvent : PluginLifecycleEvent;

/// <summary>
/// 插件错误事件
/// </summary>
public sealed class PluginErrorEvent : PluginLifecycleEvent
{
    /// <summary>
    /// 错误信息
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// 异常（可选）
    /// </summary>
    public Exception? Exception { get; init; }
}
