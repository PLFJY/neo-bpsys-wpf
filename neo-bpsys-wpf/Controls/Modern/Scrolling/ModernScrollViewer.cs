using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

// Inspired by iNKORE.UI.WPF.Modern ScrollViewerEx smooth wheel scrolling.
// This local control keeps only the project-needed behavior and avoids iNKORE theme/control dependencies.
public class ModernScrollViewer : ScrollViewer
{
    public static readonly DependencyProperty IsSmoothScrollingEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSmoothScrollingEnabled),
            typeof(bool),
            typeof(ModernScrollViewer),
            new PropertyMetadata(true));

    public static readonly DependencyProperty WheelScrollMultiplierProperty =
        DependencyProperty.Register(
            nameof(WheelScrollMultiplier),
            typeof(double),
            typeof(ModernScrollViewer),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty ScrollAnimationDurationProperty =
        DependencyProperty.Register(
            nameof(ScrollAnimationDuration),
            typeof(int),
            typeof(ModernScrollViewer),
            new PropertyMetadata((int)ScrollAnimationHelper.DefaultDuration.TotalMilliseconds));

    public static readonly DependencyProperty ScrollEasingFunctionProperty =
        DependencyProperty.Register(
            nameof(ScrollEasingFunction),
            typeof(IEasingFunction),
            typeof(ModernScrollViewer),
            new PropertyMetadata(null));

    public bool IsSmoothScrollingEnabled
    {
        get => (bool)GetValue(IsSmoothScrollingEnabledProperty);
        set => SetValue(IsSmoothScrollingEnabledProperty, value);
    }

    public double WheelScrollMultiplier
    {
        get => (double)GetValue(WheelScrollMultiplierProperty);
        set => SetValue(WheelScrollMultiplierProperty, value);
    }

    public int ScrollAnimationDuration
    {
        get => (int)GetValue(ScrollAnimationDurationProperty);
        set => SetValue(ScrollAnimationDurationProperty, value);
    }

    public IEasingFunction? ScrollEasingFunction
    {
        get => (IEasingFunction?)GetValue(ScrollEasingFunctionProperty);
        set => SetValue(ScrollEasingFunctionProperty, value);
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (TryHandleSmoothVerticalWheelScroll(this, e, WheelScrollMultiplier, ScrollAnimationDuration, IsSmoothScrollingEnabled, ScrollEasingFunction))
        {
            return;
        }

        base.OnPreviewMouseWheel(e);
    }

    internal static bool TryHandleSmoothVerticalWheelScroll(
        ScrollViewer scrollViewer,
        MouseWheelEventArgs e,
        double wheelScrollMultiplier,
        int scrollAnimationDuration,
        bool isSmoothScrollingEnabled,
        IEasingFunction? easingFunction)
    {
        if (!isSmoothScrollingEnabled
            || e.Handled
            || scrollViewer.ScrollableHeight <= 0
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            || e.Delta % Mouse.MouseWheelDeltaForOneLine != 0)
        {
            return false;
        }

        var notches = e.Delta / (double)Mouse.MouseWheelDeltaForOneLine;
        var wheelLines = Math.Max(1, SystemParameters.WheelScrollLines);
        var wheelChange = notches * wheelLines * 16 * Math.Max(0.1, wheelScrollMultiplier);
        var currentTarget = ScrollAnimationHelper.GetCurrentVerticalAnimationTarget(scrollViewer) ?? scrollViewer.VerticalOffset;
        var targetOffset = currentTarget - wheelChange;

        ScrollAnimationHelper.SmoothScrollToVerticalOffset(
            scrollViewer,
            targetOffset,
            TimeSpan.FromMilliseconds(Math.Max(0, scrollAnimationDuration)),
            animated: scrollAnimationDuration > 0,
            easingFunction);

        e.Handled = true;
        return true;
    }
}
