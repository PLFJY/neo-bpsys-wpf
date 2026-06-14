using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// 前台插件控件注册扩展方法
/// </summary>
public static class FrontedPluginControlRegistryExtensions
{
    /// <summary>
    /// 注册一个前台控件插件贡献者到 DI 容器中。
    /// </summary>
    /// <typeparam name="TContributor">插件贡献者类型，必须实现 <see cref="IFrontedControlPluginContributor"/></typeparam>
    /// <param name="services">服务容器</param>
    /// <returns>服务容器，支持链式调用</returns>
    public static IServiceCollection AddFrontedPluginControlContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IFrontedControlPluginContributor
    {
        return services.AddSingleton<IFrontedControlPluginContributor, TContributor>();
    }
}
