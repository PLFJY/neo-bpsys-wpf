using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件上下文接口 - 提供插件访问宿主应用程序功能的能力
/// Plugin context interface - Provides plugins access to host application functionality
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// 服务提供者 - 用于依赖注入
    /// Service provider for dependency injection
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// 日志工厂
    /// Logger factory
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// 插件配置目录
    /// Plugin configuration directory
    /// </summary>
    string PluginDirectory { get; }

    /// <summary>
    /// 应用程序数据目录
    /// Application data directory
    /// </summary>
    string AppDataDirectory { get; }

    /// <summary>
    /// 发送事件到事件总线
    /// Send event to event bus
    /// </summary>
    /// <typeparam name="TEvent">事件类型 Event type</typeparam>
    /// <param name="eventData">事件数据 Event data</param>
    void PublishEvent<TEvent>(TEvent eventData) where TEvent : class;

    /// <summary>
    /// 订阅事件
    /// Subscribe to event
    /// </summary>
    /// <typeparam name="TEvent">事件类型 Event type</typeparam>
    /// <param name="handler">事件处理器 Event handler</param>
    void SubscribeEvent<TEvent>(Action<TEvent> handler) where TEvent : class;
}
