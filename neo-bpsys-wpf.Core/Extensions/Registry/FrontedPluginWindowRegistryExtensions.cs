using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// 前台插件窗口注册扩展方法
/// </summary>
public static class FrontedPluginWindowRegistryExtensions
{
    /// <summary>
    /// 注册一个前台窗口插件贡献者到 DI 容器中。
    /// </summary>
    /// <typeparam name="TContributor">插件贡献者类型，必须实现 <see cref="IFrontedWindowPluginContributor"/></typeparam>
    /// <param name="services">服务容器</param>
    /// <returns>服务容器，支持链式调用</returns>
    public static IServiceCollection AddFrontedWindowPluginContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IFrontedWindowPluginContributor
    {
        return services.AddSingleton<IFrontedWindowPluginContributor, TContributor>();
    }
}
