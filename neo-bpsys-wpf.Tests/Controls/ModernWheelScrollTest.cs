#nullable enable
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

[Collection(WpfUiCollectionDefinition.Name)]
public class ModernWheelScrollTest
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    [InlineData(-120)]
    public void WheelInputMovesViewportByFractionalSystemDistance(int delta) => WpfTestThread.Run(() =>
    {
        var source = new Border { Height = 2000 };
        var viewer = new ModernScrollViewer { Content = source };
        Layout(viewer);
        var expected = WheelScrollPolicy.CalculateTarget(0, null, delta, viewer.ScrollableHeight,
            viewer.ViewportHeight, SystemParameters.WheelScrollLines, 1, continuous: true);
        var wheel = Wheel(source, delta);
        Assert.True(ModernScrollViewer.ScrollVerticalWheel(viewer, wheel, 1, 0, false, null));
        Layout(viewer);
        Assert.True(wheel.Handled);
        Assert.Equal(expected, viewer.VerticalOffset, 6);
    });

    [Fact]
    public void ZeroDeltaDoesNotMoveViewport() => WpfTestThread.Run(() =>
    {
        var source = new Border { Height = 2000 };
        var viewer = new ModernScrollViewer { Content = source };
        Layout(viewer);
        viewer.ScrollToVerticalOffset(200);
        Layout(viewer);
        Assert.True(ModernScrollViewer.ScrollVerticalWheel(viewer, Wheel(source, 0), 1, 0, false, null));
        Layout(viewer);
        Assert.Equal(200, viewer.VerticalOffset);
    });

    [Fact]
    public void SelfRegionStillUsesItsOwnViewerButProtectsOuterHost() => WpfTestThread.Run(() =>
    {
        var source = new Border { Height = 2000 };
        var inner = new ModernScrollViewer { Content = source, Height = 100 };
        var region = new Border { Child = inner };
        ModernScroll.SetOwnership(region, ModernScrollOwnership.Self);
        var outer = new ModernScrollViewer { Content = region };
        Layout(outer);
        Assert.False(Skip(inner, source, -30));
        Assert.True(Skip(outer, source, -30));
        ModernScroll.SetOwnership(region, ModernScrollOwnership.Frame);
        Assert.False(Skip(outer, source, -30));
    });

    [Fact]
    public void ExplicitNestedOwnerConsumesOnceAndHandsOffAtVisibleBoundary() => WpfTestThread.Run(() =>
    {
        var source = new Border { Height = 2000 };
        var inner = new ScrollViewer { Content = source, Height = 100 };
        ModernScroll.SetOwnership(inner, ModernScrollOwnership.Self);
        NestedSmoothScrollBehavior.SetDuration(inner, 0);
        NestedSmoothScrollBehavior.SetIsEnabled(inner, true);
        var panel = new StackPanel { Children = { inner, new Border { Height = 2000 } } };
        var outer = new ModernScrollViewer { Content = panel, ScrollAnimationDuration = 0 };
        Layout(outer);
        try
        {
            var wheel = Wheel(source, -30);
            source.RaiseEvent(wheel);
            Layout(outer);
            Assert.True(wheel.Handled);
            Assert.True(inner.VerticalOffset > 0 || SystemParameters.WheelScrollLines == 0);
            Assert.Equal(0, outer.VerticalOffset);

            inner.ScrollToBottom();
            Layout(outer);
            var handoff = Wheel(source, -30);
            source.RaiseEvent(handoff);
            Layout(outer);
            Assert.True(handoff.Handled);
            Assert.Equal(inner.ScrollableHeight, inner.VerticalOffset);
            Assert.True(outer.VerticalOffset > 0 || SystemParameters.WheelScrollLines == 0);
        }
        finally
        {
            NestedSmoothScrollBehavior.SetIsEnabled(inner, false);
            WheelScrollInput.Reset(outer);
        }
    });

    [Theory]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Shift)]
    public void ModifiedWheelIsNotClaimed(ModifierKeys modifier) => WpfTestThread.Run(() =>
    {
        var owner = new ModernScrollViewer();
        Assert.True(WheelScrollEventGuard.ShouldSkipSmoothScroll(owner, Wheel(owner, -30), owner, true, modifier));
    });

    [Fact]
    public void PopupAndOpenComboBoxKeepWheelOwnership() => WpfTestThread.Run(() =>
    {
        var owner = new ModernScrollViewer();
        Assert.True(Skip(owner, new Popup { PlacementTarget = owner }, -30));
        Assert.True(Skip(owner, new ContextMenu { PlacementTarget = owner }, -30));
        Assert.True(Skip(owner, new OpenComboBox(), -30));
    });

    [Fact]
    public void UnconstrainedListsRemainFrameContent() => WpfTestThread.Run(() =>
    {
        var list = new ListBox { Items = { "A", "B" } };
        var owner = new ModernScrollViewer { Content = list };
        Layout(owner);
        Assert.False(Skip(owner, list, -30));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptInListsScrollInPixelsAndKeepUnrealizedItems(bool dataGrid) => WpfTestThread.Run(() =>
    {
        ItemsControl list;
        if (dataGrid)
        {
            var grid = new DataGrid { AutoGenerateColumns = false };
            grid.Columns.Add(new DataGridTextColumn { Binding = new System.Windows.Data.Binding(".") });
            list = grid;
        }
        else
        {
            list = new ListBox();
        }
        list.ItemsSource = Enumerable.Range(0, 1000).ToArray();
        // Detached STA controls have no application theme resources; provide a real pixel-capable panel.
        var scrollHost = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollHost.SetValue(ScrollViewer.CanContentScrollProperty, true);
        var presenter = new FrameworkElementFactory(dataGrid ? typeof(DataGridRowsPresenter) : typeof(ItemsPresenter));
        if (dataGrid) presenter.SetValue(Panel.IsItemsHostProperty, true);
        scrollHost.AppendChild(presenter);
        list.Template = new ControlTemplate(list.GetType()) { VisualTree = scrollHost };
        if (!dataGrid)
        {
            list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        }
        NestedSmoothScrollBehavior.SetIsEnabled(list, true);
        Layout(list);
        try
        {
            var viewer = FindViewer(list)!;
            Assert.NotNull(viewer);
            var expected = WheelScrollPolicy.CalculateTarget(0, null, -30, viewer.ScrollableHeight,
                viewer.ViewportHeight, SystemParameters.WheelScrollLines, 1, true);
            Assert.True(ModernScrollViewer.ScrollVerticalWheel(viewer, Wheel(viewer, -30), 1, 0, false, null));
            Layout(list);
            Assert.Equal(expected, viewer.VerticalOffset, 6);
            Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(999));
        }
        finally
        {
            NestedSmoothScrollBehavior.SetIsEnabled(list, false);
        }
    });

    [Fact]
    public void UnloadAndDisabledSmoothModeCancelPendingAnimation() => WpfTestThread.Run(() =>
    {
        var source = new Border { Height = 2000 };
        var viewer = new ModernScrollViewer { Content = source };
        Layout(viewer);
        try
        {
            ModernScrollViewer.ScrollVerticalWheel(viewer, Wheel(source, -120), 1, 220, true, null);
            viewer.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, viewer));
            Assert.False(ScrollAnimationHelper.IsVerticalAnimationActive(viewer));
            ModernScrollViewer.ScrollVerticalWheel(viewer, Wheel(source, -120), 1, 220, true, null);
            Assert.False(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(viewer,
                Wheel(source, -30), 1, 220, false, null, source));
            Assert.False(ScrollAnimationHelper.IsVerticalAnimationActive(viewer));
        }
        finally
        {
            WheelScrollInput.Reset(viewer);
        }
    });

    private sealed class OpenComboBox : ComboBox
    {
        static OpenComboBox() => IsDropDownOpenProperty.OverrideMetadata(typeof(OpenComboBox),
            new FrameworkPropertyMetadata(true, null, (_, _) => true));
    }

    private static bool Skip(ScrollViewer owner, DependencyObject source, int delta) =>
        WheelScrollEventGuard.ShouldSkipSmoothScroll(owner, Wheel(source, delta), source, true, ModifierKeys.None);

    private static MouseWheelEventArgs Wheel(DependencyObject source, int delta) => new(Mouse.PrimaryDevice, 100, delta)
    {
        RoutedEvent = UIElement.PreviewMouseWheelEvent,
        Source = source
    };

    private static void Layout(FrameworkElement root)
    {
        root.Measure(new Size(800, 300));
        root.Arrange(new Rect(0, 0, 800, 300));
        root.UpdateLayout();
    }

    private static ScrollViewer? FindViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindViewer(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        }
        return null;
    }
}
