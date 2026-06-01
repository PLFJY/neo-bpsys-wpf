using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

public static class FrontedPluginControlRegistryExtensions
{
    public static IServiceCollection AddFrontedPluginControlContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IFrontedControlPluginContributor
    {
        return services.AddSingleton<IFrontedControlPluginContributor, TContributor>();
    }
}
