using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 为 <see cref="ScrollViewer"/> 提供平滑滚轮滚动行为的附加属性。
/// </summary>
public static class SmoothScrollBehavior
{
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(SmoothScrollState),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(null));

    /// <summary>
    /// <see cref="IsEnabledProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// <see cref="DurationProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.RegisterAttached(
            "Duration",
            typeof(int),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata((int)ScrollAnimationHelper.DefaultDuration.TotalMilliseconds));

    /// <summary>
    /// <see cref="WheelMultiplierProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty WheelMultiplierProperty =
        DependencyProperty.RegisterAttached(
            "WheelMultiplier",
            typeof(double),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(1.0));

    /// <summary>
    /// <see cref="IsProgrammaticAnimationEnabledProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsProgrammaticAnimationEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsProgrammaticAnimationEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取指定元素的 <see cref="IsEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>如果启用了平滑滚动则为 <c>true</c>。</returns>
    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="IsEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">是否启用平滑滚动。</param>
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="DurationProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>动画持续时间（毫秒）。</returns>
    public static int GetDuration(DependencyObject obj) => (int)obj.GetValue(DurationProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="DurationProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">动画持续时间（毫秒）。</param>
    public static void SetDuration(DependencyObject obj, int value) => obj.SetValue(DurationProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="WheelMultiplierProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>滚轮滚动倍率。</returns>
    public static double GetWheelMultiplier(DependencyObject obj) => (double)obj.GetValue(WheelMultiplierProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="WheelMultiplierProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">滚轮滚动倍率。</param>
    public static void SetWheelMultiplier(DependencyObject obj, double value) => obj.SetValue(WheelMultiplierProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="IsProgrammaticAnimationEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>如果启用了编程式动画则为 <c>true</c>。</returns>
    public static bool GetIsProgrammaticAnimationEnabled(DependencyObject obj) => (bool)obj.GetValue(IsProgrammaticAnimationEnabledProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="IsProgrammaticAnimationEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">是否启用编程式动画。</param>
    public static void SetIsProgrammaticAnimationEnabled(DependencyObject obj, bool value) =>
        obj.SetValue(IsProgrammaticAnimationEnabledProperty, value);

    /// <summary>
    /// 平滑滚动 <see cref="ScrollViewer"/> 到指定的垂直偏移量。
    /// </summary>
    /// <param name="scrollViewer">要滚动的 <see cref="ScrollViewer"/>。</param>
    /// <param name="targetOffset">目标垂直偏移量。</param>
    public static void SmoothScrollToVerticalOffset(ScrollViewer scrollViewer, double targetOffset)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        ScrollAnimationHelper.SmoothScrollToVerticalOffset(
            scrollViewer,
            targetOffset,
            TimeSpan.FromMilliseconds(Math.Max(0, GetDuration(scrollViewer))),
            GetIsProgrammaticAnimationEnabled(scrollViewer));
    }

    private static SmoothScrollState? GetState(DependencyObject obj) => (SmoothScrollState?)obj.GetValue(StateProperty);

    private static void SetState(DependencyObject obj, SmoothScrollState? value) => obj.SetValue(StateProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        GetState(scrollViewer)?.Detach();
        SetState(scrollViewer, null);

        if ((bool)e.NewValue)
        {
            var state = new SmoothScrollState(scrollViewer);
            SetState(scrollViewer, state);
            state.Attach();
        }
    }

    private sealed class SmoothScrollState
    {
        private readonly ScrollViewer _scrollViewer;
        private bool _isWheelHandlerAttached;
        private bool _isLifecycleAttached;

        public SmoothScrollState(ScrollViewer scrollViewer)
        {
            _scrollViewer = scrollViewer;
        }

        public void Attach()
        {
            if (!_isLifecycleAttached)
            {
                _scrollViewer.Loaded += OnLoaded;
                _scrollViewer.Unloaded += OnUnloaded;
                _isLifecycleAttached = true;
            }

            AttachWheelHandler();
        }

        public void Detach()
        {
            DetachWheelHandler();

            if (_isLifecycleAttached)
            {
                _scrollViewer.Loaded -= OnLoaded;
                _scrollViewer.Unloaded -= OnUnloaded;
                _isLifecycleAttached = false;
            }

            ScrollAnimationHelper.CancelVerticalAnimation(_scrollViewer);
        }

        private void AttachWheelHandler()
        {
            if (_isWheelHandlerAttached)
            {
                return;
            }

            _scrollViewer.MouseWheel += OnMouseWheel;
            _scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            _isWheelHandlerAttached = true;
        }

        private void DetachWheelHandler()
        {
            if (!_isWheelHandlerAttached)
            {
                return;
            }

            _scrollViewer.MouseWheel -= OnMouseWheel;
            _scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            _isWheelHandlerAttached = false;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                _scrollViewer,
                e,
                GetWheelMultiplier(_scrollViewer),
                GetDuration(_scrollViewer),
                isSmoothScrollingEnabled: true,
                easingFunction: null,
                explicitSource: e.OriginalSource as DependencyObject,
                respectExplicitSelfOwnership: true);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                _scrollViewer,
                e,
                GetWheelMultiplier(_scrollViewer),
                GetDuration(_scrollViewer),
                isSmoothScrollingEnabled: true,
                easingFunction: null,
                explicitSource: e.OriginalSource as DependencyObject,
                respectExplicitSelfOwnership: false);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachWheelHandler();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachWheelHandler();
            ScrollAnimationHelper.CancelVerticalAnimation(_scrollViewer);
        }
    }
}
