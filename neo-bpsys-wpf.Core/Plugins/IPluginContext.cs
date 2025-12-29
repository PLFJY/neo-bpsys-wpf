using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Plugins.Abstractions;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Core.Plugins.UI;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件上下文，提供插件运行时所需的所有服务和信息
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// 获取当前插件元数据
    /// </summary>
    IPluginMetadata Metadata { get; }

    /// <summary>
    /// 获取宿主应用程序服务
    /// </summary>
    IHostApplicationService HostApplication { get; }

    /// <summary>
    /// 获取插件管理器
    /// </summary>
    IPluginManager PluginManager { get; }

    /// <summary>
    /// 获取事件总线
    /// </summary>
    IPluginEventBus EventBus { get; }

    /// <summary>
    /// 获取UI扩展服务
    /// </summary>
    IUIExtensionService UIExtensions { get; }

    /// <summary>
    /// 获取配置服务
    /// </summary>
    IPluginConfigurationService Configuration { get; }

    /// <summary>
    /// 获取资源服务
    /// </summary>
    IPluginResourceService Resources { get; }

    /// <summary>
    /// 获取日志记录器
    /// </summary>
    IPluginLogger Logger { get; }

    /// <summary>
    /// 获取服务提供者
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例</returns>
    T GetService<T>() where T : class;

    /// <summary>
    /// 尝试获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例，如果不存在则返回null</returns>
    T? TryGetService<T>() where T : class;
}

/// <summary>
/// 插件上下文扩展方法
/// </summary>
public static class PluginContextExtensions
{
    /// <summary>
    /// 注册UI扩展点
    /// </summary>
    public static void RegisterUIExtension(this IPluginContext context, IUIExtensionPoint extension)
    {
        context.UIExtensions.RegisterExtension(extension);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    public static void PublishEvent<TEvent>(this IPluginContext context, TEvent pluginEvent)
        where TEvent : IPluginEvent
    {
        context.EventBus.Publish(pluginEvent);
    }

    /// <summary>
    /// 异步发布事件
    /// </summary>
    public static Task PublishEventAsync<TEvent>(this IPluginContext context, TEvent pluginEvent, CancellationToken cancellationToken = default)
        where TEvent : IPluginEvent
    {
        return context.EventBus.PublishAsync(pluginEvent, cancellationToken);
    }
}
