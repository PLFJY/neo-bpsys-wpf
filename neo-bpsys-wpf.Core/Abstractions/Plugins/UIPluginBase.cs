using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// UI插件基类 - 提供UI插件的基础实现
/// Base UI plugin class - Provides basic UI plugin implementation
/// </summary>
public abstract class UIPluginBase : PluginBase, IUIPlugin
{
    /// <inheritdoc />
    public virtual void ConfigureServices(IServiceCollection services)
    {
        OnConfigureServices(services);
    }

    /// <inheritdoc />
    public virtual IEnumerable<PluginPageDescriptor> GetPages()
    {
        return OnGetPages() ?? [];
    }

    /// <inheritdoc />
    public virtual IEnumerable<PluginControlDescriptor> GetControls()
    {
        return OnGetControls() ?? [];
    }

    /// <summary>
    /// 配置插件服务
    /// Configure plugin services
    /// </summary>
    protected virtual void OnConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// 获取插件页面
    /// Get plugin pages
    /// </summary>
    protected virtual IEnumerable<PluginPageDescriptor>? OnGetPages()
    {
        return null;
    }

    /// <summary>
    /// 获取插件控件
    /// Get plugin controls
    /// </summary>
    protected virtual IEnumerable<PluginControlDescriptor>? OnGetControls()
    {
        return null;
    }
}
