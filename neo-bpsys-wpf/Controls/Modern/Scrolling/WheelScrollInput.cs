using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>同一实际滚动宿主的输入流状态；触摸和卸载只取消滚轮动画。</summary>
internal static class WheelScrollInput
{
    private static readonly ConditionalWeakTable<ScrollViewer, WheelInputSequence> Sequences = new();

    internal static void Initialize(ScrollViewer viewer)
    {
        Sequences.GetValue(viewer, CreateSequence);
        EnableNativePanning(viewer);
    }

    internal static void EnableNativePanning(DependencyObject target)
    {
        if (DependencyPropertyHelper.GetValueSource(target, ScrollViewer.PanningModeProperty).BaseValueSource
            == BaseValueSource.Default && ScrollViewer.GetPanningMode(target) == PanningMode.None)
        {
            target.SetCurrentValue(ScrollViewer.PanningModeProperty, PanningMode.VerticalOnly);
        }
    }

    internal static bool Observe(ScrollViewer viewer, MouseWheelEventArgs e) =>
        Sequences.GetValue(viewer, CreateSequence).Observe(e.Delta, e.Timestamp);

    internal static void Reset(ScrollViewer viewer)
    {
        if (Sequences.TryGetValue(viewer, out var sequence))
        {
            sequence.Reset();
        }
        ScrollAnimationHelper.CancelVerticalAnimation(viewer);
    }

    private static WheelInputSequence CreateSequence(ScrollViewer viewer)
    {
        viewer.Unloaded += OnUnloaded;
        viewer.AddHandler(UIElement.ManipulationStartingEvent,
            new EventHandler<ManipulationStartingEventArgs>(OnManipulationStarting), handledEventsToo: true);
        return new WheelInputSequence();
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e) => Reset((ScrollViewer)sender);

    private static void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e) =>
        Reset((ScrollViewer)sender!);
}
