using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Plugins.Events;
using neo_bpsys_wpf.Plugins.Loading;
using neo_bpsys_wpf.Plugins.Services;

namespace neo_bpsys_wpf.Plugins.Hosting;

/// <summary>
/// 插件系统服务注册扩展
/// </summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// 添加插件系统服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置回调</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPluginSystem(this IServiceCollection services, Action<PluginSystemOptions>? configure = null)
    {
        var options = new PluginSystemOptions();
        configure?.Invoke(options);

        // 注册配置选项
        services.AddSingleton(options);

        // 注册事件总线（单例）
        services.AddSingleton<IPluginEventBus, PluginEventBus>();

        // 注册插件加载器
        services.AddSingleton<PluginLoader>();

        // 注册UI扩展服务
        services.AddSingleton<IUIExtensionService, UIExtensionService>();

        // 注册配置服务
        services.AddSingleton<IPluginConfigurationService>(sp =>
        {
            var opts = sp.GetRequiredService<PluginSystemOptions>();
            var configDir = Path.Combine(opts.PluginDataDirectory, "Config");
            return new PluginConfigurationService(configDir);
        });

        // 注册资源服务
        services.AddSingleton<IPluginResourceService>(sp =>
        {
            var opts = sp.GetRequiredService<PluginSystemOptions>();
            return new PluginResourceService(opts.PluginsDirectory, opts.PluginDataDirectory);
        });

        // 注册日志工厂
        services.AddSingleton<IPluginLoggerFactory, PluginLoggerFactory>();

        // 注册插件管理器
        services.AddSingleton<IPluginManager>(sp =>
        {
            var loader = sp.GetRequiredService<PluginLoader>();
            var eventBus = sp.GetRequiredService<IPluginEventBus>();
            var logger = sp.GetRequiredService<ILogger<PluginManager>>();
            var opts = sp.GetRequiredService<PluginSystemOptions>();

            return new PluginManager(loader, eventBus, sp, logger, opts.PluginsDirectory);
        });

        // 注册插件上下文工厂
        services.AddSingleton<PluginContextFactory>();

        return services;
    }

    /// <summary>
    /// 添加宿主应用程序服务
    /// </summary>
    /// <typeparam name="TService">宿主服务实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHostApplicationService<TService>(this IServiceCollection services)
        where TService : class, IHostApplicationService
    {
        services.AddSingleton<IHostApplicationService, TService>();
        return services;
    }
}

/// <summary>
/// 插件系统配置选项
/// </summary>
public class PluginSystemOptions
{
    /// <summary>
    /// 插件目录路径
    /// </summary>
    public string PluginsDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "neo-bpsys-wpf",
        "Plugins");

    /// <summary>
    /// 插件数据目录路径
    /// </summary>
    public string PluginDataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "neo-bpsys-wpf",
        "PluginData");

    /// <summary>
    /// 是否在启动时自动加载插件
    /// </summary>
    public bool AutoLoadPlugins { get; set; } = true;

    /// <summary>
    /// 是否启用热重载
    /// </summary>
    public bool EnableHotReload { get; set; } = false;

    /// <summary>
    /// 宿主应用程序版本
    /// </summary>
    public Version HostVersion { get; set; } = new Version(1, 0, 0);
}
