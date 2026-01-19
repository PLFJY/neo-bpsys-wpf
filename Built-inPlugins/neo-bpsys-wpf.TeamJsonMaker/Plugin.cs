using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;

namespace neo_bpsys_wpf.TeamJsonMaker;

public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddBackendPage<TeamJsonMaker, TeamJsonMakerViewModel>();
    }
}