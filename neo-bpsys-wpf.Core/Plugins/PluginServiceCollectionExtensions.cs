using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Plugins.Commands;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Loading;
using neo_bpsys_wpf.Core.Plugins.Settings;
using neo_bpsys_wpf.Core.Plugins.UI;
using System.IO;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件系统扩展方法
/// </summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// 添加插件系统服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPluginSystem(this IServiceCollection services)
    {
        return services.AddPluginSystem(_ => { });
    }

    /// <summary>
    /// 添加插件系统服务（带配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPluginSystem(
        this IServiceCollection services,
        Action<PluginSystemOptions> configure)
    {
        var options = new PluginSystemOptions();
        configure(options);

        services.AddSingleton(options);

        // 核心服务
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<IPluginManager, PluginManager>();

        // 扩展服务
        services.AddSingleton<IUIExtensionService, UIExtensionService>();
        services.AddSingleton<ICommandExtensionService, CommandExtensionService>();
        services.AddSingleton<IPluginEventBus, PluginEventBus>();
        services.AddSingleton<IPluginSettingsService, PluginSettingsService>();

        // 插件宿主服务
        services.AddSingleton<IPluginHost, PluginHost>();

        // 插件系统宿主服务（后台服务）
        services.AddHostedService<PluginHostService>();

        return services;
    }
}

/// <summary>
/// 插件系统配置选项
/// </summary>
public sealed class PluginSystemOptions
{
    /// <summary>
    /// 插件目录路径
    /// </summary>
    public string PluginDirectory { get; set; } = Path.Combine(AppConstants.UserDataPath, "Plugins");

    /// <summary>
    /// 是否在启动时自动加载插件
    /// </summary>
    public bool AutoLoadPlugins { get; set; } = true;

    /// <summary>
    /// 是否启用插件沙箱（程序集隔离）
    /// </summary>
    public bool EnableSandbox { get; set; } = true;

    /// <summary>
    /// 插件加载超时时间（秒）
    /// </summary>
    public int LoadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否在插件加载失败时继续加载其他插件
    /// </summary>
    public bool ContinueOnLoadError { get; set; } = true;
}
