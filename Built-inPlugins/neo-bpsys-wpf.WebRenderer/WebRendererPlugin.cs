using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.WebRenderer.Services;

namespace neo_bpsys_wpf.WebRenderer;

/// <summary>
/// 实验性 Web Renderer 插件入口。
/// </summary>
public sealed class WebRendererPlugin : PluginBase
{
    /// <inheritdoc />
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(WebRendererLaunchOptions.FromConfiguration(context.Configuration));
        services.AddSingleton<WebRendererRuntimeDetector>();
        services.AddSingleton<WebRendererBootstrapBuilder>();
        services.AddSingleton<WebRendererRuntimeStatePublisher>();
        services.AddSingleton<IWebTransitionGateway, WebTransitionGateway>();
        services.AddSingleton<IWebControlRegistry, WebControlRegistry>();
        services.AddSingleton<WebRendererSidecarService>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<WebRendererSidecarService>());
        DecorateTransitionOrchestrator(services);
    }

    private static void DecorateTransitionOrchestrator(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(item => item.ServiceType == typeof(IFrontedTransitionOrchestrator));
        if (descriptor?.Lifetime != ServiceLifetime.Singleton || descriptor.ImplementationType != typeof(FrontedTransitionOrchestrator)) return;
        services.Remove(descriptor);
        services.AddSingleton<FrontedTransitionOrchestrator>();
        services.AddSingleton<IFrontedTransitionOrchestrator, WebTransitionOrchestratorDecorator>();
    }
}
