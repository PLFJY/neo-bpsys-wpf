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
    private const int MaxSuppressedRetries = 20;
    private static readonly TimeSpan SuppressedRetryDelay = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Runs the first pending package for the given page key.
    /// </summary>
    /// <param name="owner">Loaded page or owner element.</param>
    /// <param name="pageKey">Tutorial page key.</param>
    public static async void RunPendingOnLoaded(FrameworkElement owner, string pageKey)
    {
        await RunPendingOnLoadedAsync(owner, pageKey);
    }

    private static async Task RunPendingOnLoadedAsync(FrameworkElement owner, string pageKey)
    {
        try
        {
            if (IAppHost.Host == null)
            {
                return;
            }

            var service = IAppHost.Host.Services.GetRequiredService<ITutorialService>();
            await RunOnePendingPackageAsync(service, owner, pageKey);
        }
        catch (Exception ex)
        {
            IAppHost.Host?.Services.GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(TutorialPageLoader))
                .LogWarning(ex, "Failed to run tutorial package for page {PageKey}.", pageKey);
        }
    }

    private static async Task<TutorialRunResult> RunOnePendingPackageAsync(
        ITutorialService service,
        FrameworkElement owner,
        string pageKey)
    {
        for (var attempt = 0; attempt <= MaxSuppressedRetries; attempt++)
        {
            if (!owner.IsLoaded
                || owner.Dispatcher.HasShutdownStarted
                || owner.Dispatcher.HasShutdownFinished)
            {
                return TutorialRunResult.Canceled;
            }

            var result = await service.RunPendingPagePackagesAsync(owner, pageKey, TutorialTriggerMode.AutoOnLoaded);
            if (result != TutorialRunResult.Suppressed)
            {
                return result;
            }

            await Task.Delay(SuppressedRetryDelay);
            if (!owner.IsVisible)
            {
                return TutorialRunResult.Canceled;
            }
        }

        return TutorialRunResult.Suppressed;
    }
}
