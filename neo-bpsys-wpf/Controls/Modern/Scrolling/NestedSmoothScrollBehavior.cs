using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class NestedSmoothScrollBehavior
{
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(NestedSmoothScrollState),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.RegisterAttached(
            "Duration",
            typeof(int),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata((int)ScrollAnimationHelper.DefaultDuration.TotalMilliseconds));

    public static readonly DependencyProperty WheelMultiplierProperty =
        DependencyProperty.RegisterAttached(
            "WheelMultiplier",
            typeof(double),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty UseSmoothScrollingProperty =
        DependencyProperty.RegisterAttached(
            "UseSmoothScrolling",
            typeof(bool),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(true));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static int GetDuration(DependencyObject obj) => (int)obj.GetValue(DurationProperty);

    public static void SetDuration(DependencyObject obj, int value) => obj.SetValue(DurationProperty, value);

    public static double GetWheelMultiplier(DependencyObject obj) => (double)obj.GetValue(WheelMultiplierProperty);

    public static void SetWheelMultiplier(DependencyObject obj, double value) => obj.SetValue(WheelMultiplierProperty, value);

    public static bool GetUseSmoothScrolling(DependencyObject obj) => (bool)obj.GetValue(UseSmoothScrollingProperty);

    public static void SetUseSmoothScrolling(DependencyObject obj, bool value) => obj.SetValue(UseSmoothScrollingProperty, value);

    private static NestedSmoothScrollState? GetState(DependencyObject obj) =>
        (NestedSmoothScrollState?)obj.GetValue(StateProperty);

    private static void SetState(DependencyObject obj, NestedSmoothScrollState? value) =>
        obj.SetValue(StateProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        GetState(element)?.Detach();
        SetState(element, null);

        if ((bool)e.NewValue)
        {
            var state = new NestedSmoothScrollState(element);
            SetState(element, state);
            state.Attach();
        }
    }

    private sealed class NestedSmoothScrollState
    {
        private readonly UIElement _target;
        private ScrollViewer? _scrollViewer;
        private bool _isAttached;

        public NestedSmoothScrollState(UIElement target)
        {
            _target = target;
        }

        public void Attach()
        {
            if (_isAttached)
            {
                return;
            }

            _target.PreviewMouseWheel += OnPreviewMouseWheel;
            if (_target is FrameworkElement frameworkElement)
            {
                frameworkElement.Loaded += OnLoaded;
                frameworkElement.Unloaded += OnUnloaded;
            }

            _isAttached = true;
            ResolveScrollViewer();
        }

        public void Detach()
        {
            if (!_isAttached)
            {
                return;
            }

            _target.PreviewMouseWheel -= OnPreviewMouseWheel;
            if (_target is FrameworkElement frameworkElement)
            {
                frameworkElement.Loaded -= OnLoaded;
                frameworkElement.Unloaded -= OnUnloaded;
            }

            if (_scrollViewer is not null)
            {
                ScrollAnimationHelper.CancelVerticalAnimation(_scrollViewer);
            }

            _scrollViewer = null;
            _isAttached = false;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _target.Dispatcher.BeginInvoke(ResolveScrollViewer, DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_scrollViewer is not null)
            {
                ScrollAnimationHelper.CancelVerticalAnimation(_scrollViewer);
            }
        }

        private void ResolveScrollViewer()
        {
            if (_target is Control control)
            {
                control.ApplyTemplate();
            }

            _scrollViewer = _target as ScrollViewer ?? FindDescendant<ScrollViewer>(_target);
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source
                && WheelScrollEventGuard.IsInsidePopupOrOpenComboBox(source))
            {
                return;
            }

            ResolveScrollViewer();
            if (_scrollViewer is null
                || !ModernScrollViewer.CanScrollVerticallyInWheelDirection(_scrollViewer, e.Delta))
            {
                return;
            }

            ModernScrollViewer.ScrollVerticalWheel(
                _scrollViewer,
                e,
                GetWheelMultiplier(_target),
                GetDuration(_target),
                useSmoothScrolling: GetUseSmoothScrolling(_target),
                easingFunction: null);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in EnumerateChildren(root))
        {
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static IEnumerable<DependencyObject> EnumerateChildren(DependencyObject root)
    {
        if (root is Visual or Visual3D)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                yield return VisualTreeHelper.GetChild(root, i);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
        }

        if (root is Popup { Child: not null } popup)
        {
            yield return popup.Child;
        }
    }
}
