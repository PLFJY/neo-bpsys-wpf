using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ProductTour.Controls;

internal static class OverlayHost
{
    private static readonly DependencyProperty WindowOverlayRootProperty =
        DependencyProperty.RegisterAttached(
            "WindowOverlayRoot",
            typeof(Panel),
            typeof(OverlayHost),
            new PropertyMetadata(null));

    private static readonly DependencyProperty WindowContentDialogHostProperty =
        DependencyProperty.RegisterAttached(
            "WindowContentDialogHost",
            typeof(ContentDialogHost),
            typeof(OverlayHost),
            new PropertyMetadata(null));

    public static ContentDialogHost GetContentDialogHost(FrameworkElement owner)
    {
        var window = owner as Window ?? Window.GetWindow(owner)
            ?? throw new InvalidOperationException("Unable to locate a window for the content dialog host.");
        if (ContentDialogHost.GetForWindow(window) is { } registeredHost)
        {
            return registeredHost;
        }

        if (window.GetValue(WindowContentDialogHostProperty) is ContentDialogHost existingHost
            && VisualTreeHelper.GetParent(existingHost) != null)
        {
            return existingHost;
        }

        var host = new ContentDialogHost();
        Panel.SetZIndex(host, int.MaxValue);
        GetHostPanel(window).Children.Add(host);
        window.SetValue(WindowContentDialogHostProperty, host);
        return host;
    }

    public static Panel GetHostPanel(FrameworkElement owner)
    {
        if (owner is Window window)
        {
            if (window.GetValue(WindowOverlayRootProperty) is Panel existing
                && VisualTreeHelper.GetParent(existing) != null)
            {
                return existing;
            }

            if (window.Content is Grid contentGrid)
            {
                var overlayRoot = CreateOverlayRoot();
                Grid.SetRow(overlayRoot, 0);
                Grid.SetColumn(overlayRoot, 0);
                Grid.SetRowSpan(overlayRoot, Math.Max(1, contentGrid.RowDefinitions.Count));
                Grid.SetColumnSpan(overlayRoot, Math.Max(1, contentGrid.ColumnDefinitions.Count));
                Panel.SetZIndex(overlayRoot, int.MaxValue);
                contentGrid.Children.Add(overlayRoot);
                window.SetValue(WindowOverlayRootProperty, overlayRoot);
                return overlayRoot;
            }

            if (window.Content is FrameworkElement content
                && AdornerLayer.GetAdornerLayer(content) is { } adornerLayer)
            {
                var adorner = new OverlayAdorner(content);
                adornerLayer.Add(adorner);
                window.SetValue(WindowOverlayRootProperty, adorner.OverlayRoot);
                return adorner.OverlayRoot;
            }

            throw new InvalidOperationException("Unable to locate a non-invasive window overlay host.");
        }

        if (owner is Panel ownerPanel)
        {
            return ownerPanel;
        }

        var current = owner.Parent;
        while (current is not null)
        {
            if (current is Panel panel)
            {
                return panel;
            }

            current = current is FrameworkElement element ? element.Parent : null;
        }

        if (Window.GetWindow(owner) is { } ownerWindow
            && !ReferenceEquals(ownerWindow, owner))
        {
            return GetHostPanel(ownerWindow);
        }

        throw new InvalidOperationException("Unable to locate an overlay host panel.");
    }

    private static Grid CreateOverlayRoot() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Background = null
    };

    public static Task FadeOutAndRemoveAsync(Panel host, UIElement overlay, TimeSpan duration)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation(0, duration)
        {
            From = overlay.Opacity,
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            host.Children.Remove(overlay);
            source.TrySetResult();
        };
        overlay.BeginAnimation(UIElement.OpacityProperty, animation);
        return source.Task;
    }

    private sealed class OverlayAdorner : Adorner
    {
        public OverlayAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            OverlayRoot = CreateOverlayRoot();
            AddVisualChild(OverlayRoot);
            AddLogicalChild(OverlayRoot);
        }

        public Grid OverlayRoot { get; }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => index == 0
            ? OverlayRoot
            : throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            OverlayRoot.Measure(constraint);
            return constraint;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            OverlayRoot.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
