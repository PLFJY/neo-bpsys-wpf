using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

// Inspired by iNKORE.UI.WPF.Modern ScrollViewerEx smooth wheel scrolling.
// This local control keeps only the project-needed behavior and avoids iNKORE theme/control dependencies.
/// <summary>
/// 现代滚动查看器控件，提供平滑滚轮滚动动画支持。
/// </summary>
public class ModernScrollViewer : ScrollViewer
{
    /// <summary>
    /// <see cref="IsSmoothScrollingEnabled"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsSmoothScrollingEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSmoothScrollingEnabled),
            typeof(bool),
            typeof(ModernScrollViewer),
            new PropertyMetadata(true));

    /// <summary>
    /// <see cref="WheelScrollMultiplier"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty WheelScrollMultiplierProperty =
        DependencyProperty.Register(
            nameof(WheelScrollMultiplier),
            typeof(double),
            typeof(ModernScrollViewer),
            new PropertyMetadata(1.0));

    /// <summary>
    /// <see cref="ScrollAnimationDuration"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ScrollAnimationDurationProperty =
        DependencyProperty.Register(
            nameof(ScrollAnimationDuration),
            typeof(int),
            typeof(ModernScrollViewer),
            new PropertyMetadata((int)ScrollAnimationHelper.DefaultDuration.TotalMilliseconds));

    /// <summary>
    /// <see cref="ScrollEasingFunction"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ScrollEasingFunctionProperty =
        DependencyProperty.Register(
            nameof(ScrollEasingFunction),
            typeof(IEasingFunction),
            typeof(ModernScrollViewer),
            new PropertyMetadata(null));

    /// <summary>
    /// 初始化 <see cref="ModernScrollViewer"/> 的新实例。
    /// </summary>
    public ModernScrollViewer()
    {
        PreviewMouseWheel += OnPreviewMouseWheel;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 获取或设置一个值，指示是否启用平滑滚动。
    /// </summary>
    public bool IsSmoothScrollingEnabled
    {
        get => (bool)GetValue(IsSmoothScrollingEnabledProperty);
        set => SetValue(IsSmoothScrollingEnabledProperty, value);
    }

    /// <summary>
    /// 获取或设置滚轮滚动倍率。
    /// </summary>
    public double WheelScrollMultiplier
    {
        get => (double)GetValue(WheelScrollMultiplierProperty);
        set => SetValue(WheelScrollMultiplierProperty, value);
    }

    /// <summary>
    /// 获取或设置滚动动画持续时间（毫秒）。
    /// </summary>
    public int ScrollAnimationDuration
    {
        get => (int)GetValue(ScrollAnimationDurationProperty);
        set => SetValue(ScrollAnimationDurationProperty, value);
    }

    /// <summary>
    /// 获取或设置滚动动画的缓动函数。
    /// </summary>
    public IEasingFunction? ScrollEasingFunction
    {
        get => (IEasingFunction?)GetValue(ScrollEasingFunctionProperty);
        set => SetValue(ScrollEasingFunctionProperty, value);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ScrollAnimationHelper.CancelVerticalAnimation(this);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TryHandleSmoothVerticalWheelScroll(
            this,
            e,
            WheelScrollMultiplier,
            ScrollAnimationDuration,
            IsSmoothScrollingEnabled,
            ScrollEasingFunction,
            e.OriginalSource as DependencyObject,
            respectExplicitSelfOwnership: true);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (TryHandleSmoothVerticalWheelScroll(
                this,
                e,
                WheelScrollMultiplier,
                ScrollAnimationDuration,
                IsSmoothScrollingEnabled,
                ScrollEasingFunction,
                e.OriginalSource as DependencyObject,
                respectExplicitSelfOwnership: false))
        {
            return;
        }

        base.OnMouseWheel(e);
    }

    internal static bool TryHandleSmoothVerticalWheelScroll(
        ScrollViewer scrollViewer,
        MouseWheelEventArgs e,
        double wheelScrollMultiplier,
        int scrollAnimationDuration,
        bool isSmoothScrollingEnabled,
        IEasingFunction? easingFunction)
    {
        return TryHandleSmoothVerticalWheelScroll(
            scrollViewer,
            e,
            wheelScrollMultiplier,
            scrollAnimationDuration,
            isSmoothScrollingEnabled,
            easingFunction,
            explicitSource: null);
    }

    internal static bool TryHandleSmoothVerticalWheelScroll(
        ScrollViewer scrollViewer,
        MouseWheelEventArgs e,
        double wheelScrollMultiplier,
        int scrollAnimationDuration,
        bool isSmoothScrollingEnabled,
        IEasingFunction? easingFunction,
        DependencyObject? explicitSource)
    {
        return TryHandleSmoothVerticalWheelScroll(
            scrollViewer,
            e,
            wheelScrollMultiplier,
            scrollAnimationDuration,
            isSmoothScrollingEnabled,
            easingFunction,
            explicitSource,
            respectExplicitSelfOwnership: true);
    }

    internal static bool TryHandleSmoothVerticalWheelScroll(
        ScrollViewer scrollViewer,
        MouseWheelEventArgs e,
        double wheelScrollMultiplier,
        int scrollAnimationDuration,
        bool isSmoothScrollingEnabled,
        IEasingFunction? easingFunction,
        DependencyObject? explicitSource,
        bool respectExplicitSelfOwnership)
    {
        if (WheelScrollEventGuard.ShouldSkipSmoothScroll(scrollViewer, e, explicitSource, respectExplicitSelfOwnership)
            || !isSmoothScrollingEnabled)
        {
            return false;
        }

        return ScrollVerticalWheel(
            scrollViewer,
            e,
            wheelScrollMultiplier,
            scrollAnimationDuration,
            useSmoothScrolling: true,
            easingFunction);
    }

    internal static bool ScrollVerticalWheel(
        ScrollViewer scrollViewer,
        MouseWheelEventArgs e,
        double wheelScrollMultiplier,
        int scrollAnimationDuration,
        bool useSmoothScrolling,
        IEasingFunction? easingFunction)
    {
        if (e.Handled
            || scrollViewer.ScrollableHeight <= 0
            || !CanScrollVerticallyInWheelDirection(scrollViewer, e.Delta)
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
            animated: useSmoothScrolling && scrollAnimationDuration > 0,
            easingFunction);

        e.Handled = true;
        return true;
    }

    internal static bool CanScrollVerticallyInWheelDirection(ScrollViewer scrollViewer, int wheelDelta)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        var currentOffset = ScrollAnimationHelper.GetCurrentVerticalAnimationTarget(scrollViewer) ?? scrollViewer.VerticalOffset;
        return wheelDelta switch
        {
            < 0 => currentOffset < scrollViewer.ScrollableHeight,
            > 0 => currentOffset > 0,
            _ => false
        };
    }
}
