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
        services.TryAddSingleton<ITutorialService, TutorialService>();
        services.TryAddSingleton<IOnboardingCoordinator, OnboardingCoordinator>();
        services.TryAddSingleton<IGameTutorialSandboxService, NoOpGameTutorialSandboxService>();
        services.TryAddSingleton<ITutorialLanguageService, NoOpTutorialLanguageService>();
        return services;
    }
}
