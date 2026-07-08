using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using System.Windows;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Runs page tutorial packages from existing WPF page Loaded events.
/// </summary>
public static class TutorialPageLoader
{
    private const int MaxDrainSequencePackages = 32;

    /// <summary>
    /// Runs the first pending package for the given page key.
    /// </summary>
    /// <param name="owner">Loaded page or owner element.</param>
    /// <param name="pageKey">Tutorial page key.</param>
    public static async void RunPendingOnLoaded(FrameworkElement owner, string pageKey)
    {
        await RunPendingOnLoadedCoreAsync(owner, pageKey, "AutoOnLoaded");
    }

    /// <summary>
    /// Runs pending tutorial packages for a loaded owner and records the UI event reason for diagnostics.
    /// </summary>
    /// <param name="owner">Loaded page or owner element.</param>
    /// <param name="pageKey">Tutorial page key.</param>
    /// <param name="reason">UI event or caller reason.</param>
    public static async void RunPendingOnLoaded(FrameworkElement owner, string pageKey, string reason)
    {
        await RunPendingOnLoadedCoreAsync(owner, pageKey, reason);
    }

    private static async Task RunPendingOnLoadedAsync(FrameworkElement owner, string pageKey) =>
        await RunPendingOnLoadedCoreAsync(owner, pageKey, "AutoOnLoaded");

    private static async Task RunPendingOnLoadedCoreAsync(FrameworkElement owner, string pageKey, string reason)
    {
        var ownerType = owner.GetType().Name;
        ITutorialRunObserver? observer = null;
        try
        {
            if (IAppHost.Host == null)
            {
                return;
            }

            var service = IAppHost.Host.Services.GetRequiredService<ITutorialService>();
            var sequenceRegistry = IAppHost.Host.Services.GetService<ITutorialSequenceRegistry>();
            var activationService = IAppHost.Host.Services.GetService<ITutorialOwnerActivationService>()
                ?? new AlwaysActiveTutorialOwnerActivationService();
            observer = IAppHost.Host.Services.GetService<ITutorialRunObserver>();
            var sequenceDefinition = sequenceRegistry?.GetSequenceDefinition(pageKey)
                ?? new TutorialSequenceDefinition { PageKey = pageKey };
            var strategy = sequenceDefinition.AutoRunStrategy;
            observer?.OnAutoRunRequested(ownerType, pageKey, reason);
            observer?.OnSequenceResolved(pageKey, sequenceDefinition.PackageIds, strategy);

            if (!IsOwnerActive(activationService, observer, owner, pageKey, ownerType, reason))
            {
                observer?.OnAutoRunCompleted(ownerType, pageKey, TutorialRunResult.Canceled);
                return;
            }

            if (strategy == TutorialAutoRunStrategy.SinglePendingPackage)
            {
                var result = await RunOnePendingPackageAsync(service, owner, pageKey);
                observer?.OnAutoRunCompleted(ownerType, pageKey, result);
                return;
            }

            var finalResult = TutorialRunResult.NotPending;
            for (var completedPackages = 0; completedPackages < MaxDrainSequencePackages; completedPackages++)
            {
                if (strategy == TutorialAutoRunStrategy.ContinueWhileActive
                    && !IsOwnerActive(activationService, observer, owner, pageKey, ownerType, "Continuation"))
                {
                    observer?.OnAutoRunCompleted(ownerType, pageKey, TutorialRunResult.Canceled);
                    return;
                }

                var result = await RunOnePendingPackageAsync(service, owner, pageKey);
                finalResult = result;
                if (result != TutorialRunResult.Completed)
                {
                    observer?.OnAutoRunCompleted(ownerType, pageKey, result);
                    return;
                }

                if (strategy == TutorialAutoRunStrategy.ContinueWhileActive)
                {
                    await owner.Dispatcher.InvokeAsync(
                        () => { },
                        System.Windows.Threading.DispatcherPriority.ContextIdle);
                    if (!IsOwnerActive(activationService, observer, owner, pageKey, ownerType, "Continuation"))
                    {
                        observer?.OnAutoRunCompleted(ownerType, pageKey, TutorialRunResult.Canceled);
                        return;
                    }

                    var nextPending = await service.GetNextPendingPackageAsync(owner, pageKey);
                    if (nextPending is null)
                    {
                        observer?.OnAutoRunCompleted(ownerType, pageKey, TutorialRunResult.NotPending);
                        return;
                    }
                }
            }

            observer?.OnAutoRunCompleted(ownerType, pageKey, finalResult);
        }
        catch (Exception ex)
        {
            IAppHost.Host?.Services.GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(TutorialPageLoader))
                .LogWarning(ex, "Failed to run tutorial package for page {PageKey}.", pageKey);
            observer?.OnAutoRunCompleted(ownerType, pageKey, TutorialRunResult.Failed);
        }
    }

    private static async Task<TutorialRunResult> RunOnePendingPackageAsync(
        ITutorialService service,
        FrameworkElement owner,
        string pageKey)
    {
        if (!owner.IsLoaded
            || owner.Dispatcher.HasShutdownStarted
            || owner.Dispatcher.HasShutdownFinished)
        {
            return TutorialRunResult.Canceled;
        }

        return await service.RunPendingPagePackagesAsync(
            owner,
            pageKey,
            TutorialTriggerMode.AutoOnLoaded);
    }

    private static bool IsOwnerActive(
        ITutorialOwnerActivationService activationService,
        ITutorialRunObserver? observer,
        FrameworkElement owner,
        string pageKey,
        string ownerType,
        string reason)
    {
        if (activationService.IsOwnerActive(owner, pageKey))
        {
            return true;
        }

        observer?.OnAutoRunRejectedInactiveOwner(ownerType, pageKey, reason);
        return false;
    }
}
