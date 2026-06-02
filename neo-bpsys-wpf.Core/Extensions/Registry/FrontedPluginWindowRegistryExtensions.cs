using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

public static class FrontedPluginWindowRegistryExtensions
{
    public static IServiceCollection AddFrontedWindowPluginContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IFrontedWindowPluginContributor
    {
        return services.AddSingleton<IFrontedWindowPluginContributor, TContributor>();
    }
}
