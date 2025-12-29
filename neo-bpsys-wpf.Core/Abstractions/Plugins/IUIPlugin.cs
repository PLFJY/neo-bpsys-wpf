using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// UI插件接口 - 为应用程序提供UI扩展能力的插件
/// UI Plugin interface - Plugins that provide UI extension capabilities
/// </summary>
public interface IUIPlugin : IPlugin
{
    /// <summary>
    /// 配置插件服务
    /// Configure plugin services for dependency injection
    /// </summary>
    /// <param name="services">服务集合 Service collection</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// 获取插件提供的页面类型列表
    /// Get list of page types provided by the plugin
    /// </summary>
    /// <returns>页面类型列表 List of page types</returns>
    IEnumerable<PluginPageDescriptor> GetPages();

    /// <summary>
    /// 获取插件提供的自定义控件类型列表
    /// Get list of custom control types provided by the plugin
    /// </summary>
    /// <returns>控件类型列表 List of control types</returns>
    IEnumerable<PluginControlDescriptor> GetControls();
}
