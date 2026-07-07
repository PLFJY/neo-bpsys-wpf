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
    /// <summary>
    /// Runs the first pending package for the given page key.
    /// </summary>
    /// <param name="owner">Loaded page or owner element.</param>
    /// <param name="pageKey">Tutorial page key.</param>
    public static async void RunPendingOnLoaded(FrameworkElement owner, string pageKey)
    {
        try
        {
            if (IAppHost.Host == null)
            {
                return;
            }

            var service = IAppHost.Host.Services.GetRequiredService<ITutorialService>();
            await service.RunPendingPagePackagesAsync(owner, pageKey, TutorialTriggerMode.AutoOnLoaded);
        }
        catch (Exception ex)
        {
            IAppHost.Host?.Services.GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(TutorialPageLoader))
                .LogWarning(ex, "Failed to run tutorial package for page {PageKey}.", pageKey);
        }
    }
}
