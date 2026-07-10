using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Service collection extensions for Product Tour.
/// </summary>
public static class ProductTourServiceCollectionExtensions
{
    /// <summary>
    /// Registers Product Tour services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddProductTour(this IServiceCollection services)
    {
        services.TryAddSingleton<ProductTourOptions>();
        services.TryAddSingleton<ITutorialPackageRegistry, TutorialPackageRegistry>();
        services.TryAddSingleton<ITutorialSequenceRegistry, TutorialSequenceRegistry>();
        services.TryAddSingleton<ITutorialFlowRegistry, TutorialFlowRegistry>();
        services.TryAddSingleton<ITutorialSignalService, TutorialSignalService>();
        services.TryAddSingleton<ITutorialStateStore, TutorialStateStore>();
        services.TryAddSingleton<ITutorialTextProvider, DefaultTutorialTextProvider>();
        services.TryAddSingleton<ITutorialRunObserver, NoOpTutorialRunObserver>();
        services.TryAddSingleton<ITutorialPlaybackCoordinator, TutorialPlaybackCoordinator>();
        services.TryAddSingleton<ITutorialRegistrationService, TutorialRegistrationService>();
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
            sp.GetRequiredService<ProductTourOptions>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TutorialService>>()));
        services.TryAddSingleton<ITutorialStateManager>(sp => sp.GetRequiredService<TutorialService>());
        services.TryAddSingleton<ITutorialStepCancellation>(sp => sp.GetRequiredService<TutorialService>());
        services.TryAddSingleton<ITutorialRunner>(sp => new TutorialRunner(
            sp.GetRequiredService<TutorialService>(),
            sp.GetRequiredService<ITutorialPlaybackCoordinator>(),
            sp.GetRequiredService<ITutorialPackageRegistry>(),
            sp.GetRequiredService<ITutorialFlowRegistry>(),
            sp.GetRequiredService<ITutorialStateStore>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TutorialRunner>>()));
        services.TryAddSingleton<IOnboardingCoordinator, OnboardingCoordinator>();
        services.TryAddSingleton<IGameTutorialSandboxService, NoOpGameTutorialSandboxService>();
        services.TryAddSingleton<ITutorialLanguageService, NoOpTutorialLanguageService>();
        services.TryAddSingleton<ITutorialAvatarProvider, NoOpTutorialAvatarProvider>();
        return services;
    }
}
