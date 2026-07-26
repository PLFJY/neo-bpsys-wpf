using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions;

/// <summary>
/// 插件基类。所有插件均应当继承自此类。
/// </summary>
public abstract class PluginBase
{
    /// <summary>
    /// 当前插件的设置目录。插件的各项设置应当存放在此目录中。
    /// </summary>
    public string PluginConfigFolder { get; internal set; } = "";

    /// <summary>
    /// 初始化插件。一般在这个方法中完成插件的各项服务的注册。
    /// </summary>
    /// <param name="context">宿主构建上下文，包含配置和环境信息</param>
    /// <param name="services">依赖注入服务集合，用于注册插件服务</param>
    public abstract void Initialize(HostBuilderContext context, IServiceCollection services);

    /// <summary>
    /// 当前插件的信息
    /// </summary>
    public PluginInfo Info { get; set; } = null!;
}
