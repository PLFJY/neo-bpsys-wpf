using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class SmoothScrollBehavior
{
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(SmoothScrollState),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.RegisterAttached(
            "Duration",
            typeof(int),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata((int)ScrollAnimationHelper.DefaultDuration.TotalMilliseconds));

    public static readonly DependencyProperty WheelMultiplierProperty =
        DependencyProperty.RegisterAttached(
            "WheelMultiplier",
            typeof(double),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty IsProgrammaticAnimationEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsProgrammaticAnimationEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(true));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static int GetDuration(DependencyObject obj) => (int)obj.GetValue(DurationProperty);

    public static void SetDuration(DependencyObject obj, int value) => obj.SetValue(DurationProperty, value);

    public static double GetWheelMultiplier(DependencyObject obj) => (double)obj.GetValue(WheelMultiplierProperty);

    public static void SetWheelMultiplier(DependencyObject obj, double value) => obj.SetValue(WheelMultiplierProperty, value);

    public static bool GetIsProgrammaticAnimationEnabled(DependencyObject obj) => (bool)obj.GetValue(IsProgrammaticAnimationEnabledProperty);

    public static void SetIsProgrammaticAnimationEnabled(DependencyObject obj, bool value) =>
        obj.SetValue(IsProgrammaticAnimationEnabledProperty, value);

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
