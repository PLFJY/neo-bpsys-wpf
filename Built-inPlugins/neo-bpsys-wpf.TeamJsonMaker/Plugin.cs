using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;

namespace neo_bpsys_wpf.TeamJsonMaker;

/// <summary>队伍 JSON 信息制作插件入口。</summary>
public class Plugin : PluginBase
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="services"/> 为 <see langword="null"/>。</exception>
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddBackendPage<TeamJsonMaker, TeamJsonMakerViewModel>();
    }
}