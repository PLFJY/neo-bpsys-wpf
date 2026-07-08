using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// WPF attached properties for declarative Product Tour page integration.
/// </summary>
public static class Tutorial
{
    /// <summary>Identifies the PageKey attached property.</summary>
    public static readonly DependencyProperty PageKeyProperty = DependencyProperty.RegisterAttached(
        "PageKey",
        typeof(string),
        typeof(Tutorial),
        new PropertyMetadata(null));

    /// <summary>Identifies the AutoRunOnLoaded attached property.</summary>
    public static readonly DependencyProperty AutoRunOnLoadedProperty = DependencyProperty.RegisterAttached(
        "AutoRunOnLoaded",
        typeof(bool),
        typeof(Tutorial),
        new PropertyMetadata(false, OnAutoRunOnLoadedChanged));

    /// <summary>Gets the tutorial page key for an element.</summary>
    /// <param name="element">Target element.</param>
    /// <returns>The page key.</returns>
    public static string? GetPageKey(DependencyObject element) => (string?)element.GetValue(PageKeyProperty);

    /// <summary>Sets the tutorial page key for an element.</summary>
    /// <param name="element">Target element.</param>
    /// <param name="value">Page key.</param>
    public static void SetPageKey(DependencyObject element, string? value) => element.SetValue(PageKeyProperty, value);

    /// <summary>Gets whether tutorials should run automatically when the element is loaded.</summary>
    /// <param name="element">Target element.</param>
    /// <returns><see langword="true" /> when auto-run is enabled.</returns>
    public static bool GetAutoRunOnLoaded(DependencyObject element) => (bool)element.GetValue(AutoRunOnLoadedProperty);

    /// <summary>Sets whether tutorials should run automatically when the element is loaded.</summary>
    /// <param name="element">Target element.</param>
    /// <param name="value">Whether auto-run is enabled.</param>
    public static void SetAutoRunOnLoaded(DependencyObject element, bool value) => element.SetValue(AutoRunOnLoadedProperty, value);

    private static void OnAutoRunOnLoadedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)args.OldValue)
        {
            element.Loaded -= OnLoaded;
        }

        if ((bool)args.NewValue)
        {
            element.Loaded += OnLoaded;
        }
    }

    private static async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement owner)
        {
            return;
        }

        var pageKey = GetPageKey(owner);
        if (string.IsNullOrWhiteSpace(pageKey) || IAppHost.Host == null)
        {
            return;
        }

        try
        {
            var service = IAppHost.Host.Services.GetRequiredService<ITutorialService>();
            await service.RunPendingPagePackagesAsync(owner, pageKey, TutorialTriggerMode.AutoOnLoaded);
        }
        catch (Exception ex)
        {
            IAppHost.Host?.Services.GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(Tutorial))
                .LogWarning(ex, "Failed to run tutorial package for page {PageKey}.", pageKey);
        }
    }
}
