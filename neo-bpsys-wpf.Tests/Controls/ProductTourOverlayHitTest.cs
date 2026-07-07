#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// Tests Product Tour overlay hit-test behavior.
/// </summary>
public sealed class ProductTourOverlayHitTest
{
    [Fact]
    public async Task AllowTargetOnlyLeavesTargetHitTestReachable()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var host = CreateOwnerWithTarget();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var overlay = await ShowOverlayAsync(host.Owner, host.Target, ProductTourInteractionMode.AllowTargetOnly, cts.Token);

                var hit = HitTestTargetCenter(host.Owner, host.Target);

                cts.Cancel();
                await overlay.Task;
                Assert.NotNull(hit);
                Assert.True(
                    IsDescendantOf(hit, host.Target),
                    $"Expected target hit, got {DescribeHit(hit, host.Target, overlay.Overlay)}.");
            }
            finally
            {
                host.Window.Close();
            }
        });
    }

    [Fact]
    public async Task BlockAllInterceptsTargetHitTest()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var host = CreateOwnerWithTarget();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var overlay = await ShowOverlayAsync(host.Owner, host.Target, ProductTourInteractionMode.BlockAll, cts.Token);

                var hit = HitTestTargetCenter(host.Owner, host.Target);

                cts.Cancel();
                await overlay.Task;
                Assert.NotNull(hit);
                Assert.False(
                    IsDescendantOf(hit, host.Target),
                    $"Expected overlay mask hit, got target descendant {DescribeHit(hit, host.Target, overlay.Overlay)}.");
                Assert.True(
                    IsDescendantOf(hit, overlay.Overlay),
                    $"Expected overlay mask hit, got {DescribeHit(hit, host.Target, overlay.Overlay)}.");
            }
            finally
            {
                host.Window.Close();
            }
        });
    }

    private static TestHost CreateOwnerWithTarget()
    {
        var owner = new Grid
        {
            Width = 800,
            Height = 600,
            Background = Brushes.White
        };
        var target = new Button
        {
            Name = "TargetButton",
            Content = "Target",
            Width = 120,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(40)
        };
        owner.Children.Add(target);

        var window = new Window
        {
            Width = 800,
            Height = 600,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Left = -10000,
            Top = -10000,
            Content = owner
        };
        window.Show();
        window.UpdateLayout();
        return new TestHost(window, owner, target);
    }

    private static async Task<ShownOverlay> ShowOverlayAsync(
        Grid owner,
        Button target,
        ProductTourInteractionMode interactionMode,
        CancellationToken cancellationToken)
    {
        var overlay = new ProductTourOverlay();
        owner.Children.Add(overlay);
        owner.UpdateLayout();
        var runTask = overlay.ShowStepAsync(
            new ProductTourStep
            {
                Title = "Title",
                Description = "Description",
                Placement = ProductTourPlacement.Right,
                InteractionMode = interactionMode
            },
            target,
            new ProductTourStepContext
            {
                Owner = owner,
                StepIndex = 0,
                StepCount = 1
            },
            cancellationToken);
        await Task.Delay(350, cancellationToken);
        owner.UpdateLayout();
        return new ShownOverlay(overlay, runTask);
    }

    private static DependencyObject? HitTestTargetCenter(Grid owner, Button target)
    {
        var center = target.TranslatePoint(new Point(target.ActualWidth / 2, target.ActualHeight / 2), owner);
        return owner.InputHitTest(center) as DependencyObject;
    }

    private static bool IsDescendantOf(DependencyObject current, DependencyObject ancestor)
    {
        if (ReferenceEquals(current, ancestor))
        {
            return true;
        }

        var parent = VisualTreeHelper.GetParent(current);
        while (parent != null)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }

            if (parent is FrameworkElement { TemplatedParent: not null } parentElement &&
                ReferenceEquals(parentElement.TemplatedParent, ancestor))
            {
                return true;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        if (current is FrameworkElement { TemplatedParent: not null } currentElement &&
            ReferenceEquals(currentElement.TemplatedParent, ancestor))
        {
            return true;
        }

        return false;
    }

    private static string DescribeHit(DependencyObject hit, Button target, ProductTourOverlay overlay)
    {
        var parts = new System.Collections.Generic.List<string>();
        var current = hit;
        while (current != null)
        {
            var name = current is FrameworkElement element && !string.IsNullOrWhiteSpace(element.Name)
                ? $"#{element.Name}"
                : string.Empty;
            var targetMarker = ReferenceEquals(current, target) ? " target" : string.Empty;
            var overlayMarker = ReferenceEquals(current, overlay) ? " overlay" : string.Empty;
            parts.Add($"{current.GetType().FullName}{name}{targetMarker}{overlayMarker}");
            current = VisualTreeHelper.GetParent(current);
        }

        return string.Join(" <- ", parts);
    }

    private sealed record ShownOverlay(ProductTourOverlay Overlay, Task<ProductTourStepAction> Task);

    private sealed record TestHost(Window Window, Grid Owner, Button Target);
}
