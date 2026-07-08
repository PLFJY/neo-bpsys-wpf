using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<ITutorialPackageRegistry, TutorialPackageRegistry>();
        services.AddSingleton<ITutorialSequenceRegistry, TutorialSequenceRegistry>();
        services.AddSingleton<ITutorialFlowRegistry, TutorialFlowRegistry>();
        services.AddSingleton<ITutorialSignalService, TutorialSignalService>();
        services.AddSingleton<ITutorialStateStore, TutorialStateStore>();
        services.AddSingleton<ITutorialTextProvider, DefaultTutorialTextProvider>();
        services.AddSingleton<ITutorialService, TutorialService>();
        services.AddSingleton<IOnboardingCoordinator, OnboardingCoordinator>();
        services.AddSingleton<IGameTutorialSandboxService, NoOpGameTutorialSandboxService>();
        services.AddSingleton<ITutorialLanguageService, NoOpTutorialLanguageService>();
        return services;
    }
}
