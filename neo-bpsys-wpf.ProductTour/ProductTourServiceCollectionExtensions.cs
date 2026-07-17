using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 产品导览的服务集合扩展。
/// </summary>
public static class ProductTourServiceCollectionExtensions
{
    /// <summary>
    /// 注册产品导览服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">可选的产品导览配置委托。</param>
    /// <returns>同一服务集合。</returns>
    public static IServiceCollection AddProductTour(this IServiceCollection services, Action<ProductTourOptions>? configure = null)
    {
        var options = new ProductTourOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddSingleton<ITutorialPackageRegistry, TutorialPackageRegistry>();
        services.TryAddSingleton<ITutorialSequenceRegistry, TutorialSequenceRegistry>();
        services.TryAddSingleton<ITutorialFlowRegistry, TutorialFlowRegistry>();
        services.TryAddSingleton<ITutorialSignalService, TutorialSignalService>();
        services.TryAddSingleton<ITutorialStateStore, TutorialStateStore>();
        services.TryAddSingleton<ITutorialSessionSuppression, TutorialSessionSuppression>();
        services.TryAddSingleton<ITutorialTextProvider, DefaultTutorialTextProvider>();
        services.TryAddSingleton<TutorialDebugService>();
        services.TryAddSingleton<ITutorialDebugService>(sp => sp.GetRequiredService<TutorialDebugService>());
        services.TryAddSingleton<ITutorialRunObserver>(sp => sp.GetRequiredService<ITutorialDebugService>());
        services.TryAddSingleton<ITutorialPlaybackCoordinator, TutorialPlaybackCoordinator>();
        services.TryAddSingleton<ITutorialRegistrationService, TutorialRegistrationService>();
        services.TryAddSingleton<ITutorialContentResolver, DefaultTutorialContentResolver>();
        services.TryAddSingleton(sp => new TutorialService(
            sp,
            sp.GetRequiredService<ITutorialPackageRegistry>(),
            sp.GetRequiredService<ITutorialSequenceRegistry>(),
            sp.GetRequiredService<ITutorialFlowRegistry>(),
            sp.GetRequiredService<ITutorialStateStore>(),
            sp.GetRequiredService<ITutorialSignalService>(),
            sp.GetRequiredService<ITutorialTextProvider>(),
            sp.GetRequiredService<ITutorialAvatarProvider>(),
            sp.GetRequiredService<ITutorialRunObserver>(),
            sp.GetRequiredService<ITutorialContentResolver>(),
            sp.GetRequiredService<ITutorialLanguageService>(),
            sp.GetRequiredService<ProductTourOptions>(),
            sp.GetRequiredService<ITutorialSessionSuppression>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TutorialService>>(),
            sp.GetRequiredService<ITutorialDebugService>()));
        services.TryAddSingleton<ITutorialStateManager>(sp => sp.GetRequiredService<TutorialService>());
        services.TryAddSingleton<ITutorialStepCancellation>(sp => sp.GetRequiredService<TutorialService>());
        services.TryAddSingleton<ITutorialRunner>(sp => new TutorialRunner(
            sp.GetRequiredService<TutorialService>(),
            sp.GetRequiredService<ITutorialPlaybackCoordinator>(),
            sp.GetRequiredService<ITutorialPackageRegistry>(),
            sp.GetRequiredService<ITutorialFlowRegistry>(),
            sp.GetRequiredService<ITutorialStateStore>(),
            sp.GetRequiredService<ITutorialSessionSuppression>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TutorialRunner>>(),
            sp.GetRequiredService<ITutorialDebugService>()));
        services.TryAddSingleton<IOnboardingCoordinator, OnboardingCoordinator>();
        services.TryAddSingleton<IGameTutorialSandboxService, NoOpGameTutorialSandboxService>();
        services.TryAddSingleton<ITutorialLanguageService, NoOpTutorialLanguageService>();
        services.TryAddSingleton<ITutorialAvatarProvider, NoOpTutorialAvatarProvider>();
        return services;
    }
}
