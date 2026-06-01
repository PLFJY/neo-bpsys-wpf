using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;

namespace neo_bpsys_wpf.ExampleFrontedControls;

public sealed class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddFrontedPluginControlContributor<TeamCardFrontedControlContributor>();
    }
}
