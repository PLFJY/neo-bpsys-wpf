using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class ComboBoxDropdownSmoothScrollBehavior
{
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(ComboBoxDropdownSmoothScrollState),
            typeof(ComboBoxDropdownSmoothScrollBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ComboBoxDropdownSmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.RegisterAttached(
            "Duration",
            typeof(int),
            typeof(ComboBoxDropdownSmoothScrollBehavior),
            new PropertyMetadata((int)ScrollAnimationHelper.DefaultDuration.TotalMilliseconds));

    public static readonly DependencyProperty WheelMultiplierProperty =
        DependencyProperty.RegisterAttached(
            "WheelMultiplier",
            typeof(double),
            typeof(ComboBoxDropdownSmoothScrollBehavior),
            new PropertyMetadata(1.0));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static int GetDuration(DependencyObject obj) => (int)obj.GetValue(DurationProperty);

    public static void SetDuration(DependencyObject obj, int value) => obj.SetValue(DurationProperty, value);

    public static double GetWheelMultiplier(DependencyObject obj) => (double)obj.GetValue(WheelMultiplierProperty);

    public static void SetWheelMultiplier(DependencyObject obj, double value) => obj.SetValue(WheelMultiplierProperty, value);

    private static ComboBoxDropdownSmoothScrollState? GetState(DependencyObject obj) =>
        (ComboBoxDropdownSmoothScrollState?)obj.GetValue(StateProperty);

    private static void SetState(DependencyObject obj, ComboBoxDropdownSmoothScrollState? value) =>
        obj.SetValue(StateProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox comboBox)
        {
            return;
        }

        GetState(comboBox)?.Detach();
        SetState(comboBox, null);

        if ((bool)e.NewValue)
        {
            var state = new ComboBoxDropdownSmoothScrollState(comboBox);
            SetState(comboBox, state);
            state.Attach();
        }
    }

    private sealed class ComboBoxDropdownSmoothScrollState
    {
        private readonly ComboBox _comboBox;
        private Popup? _popup;
        private UIElement? _handlerElement;
        private ScrollViewer? _scrollViewer;
        private bool _isAttached;

        public ComboBoxDropdownSmoothScrollState(ComboBox comboBox)
        {
            _comboBox = comboBox;
        }

        public void Attach()
        {
            if (_isAttached)
            {
                return;
            }

            _comboBox.DropDownOpened += OnDropDownOpened;
            _comboBox.DropDownClosed += OnDropDownClosed;
            _comboBox.Unloaded += OnUnloaded;
            _isAttached = true;
        }

        public void Detach()
        {
            if (!_isAttached)
            {
                return;
            }

            DetachWheelHandler();
            _comboBox.DropDownOpened -= OnDropDownOpened;
            _comboBox.DropDownClosed -= OnDropDownClosed;
            _comboBox.Unloaded -= OnUnloaded;
            _isAttached = false;
        }

        private void OnDropDownOpened(object? sender, EventArgs e)
        {
            _comboBox.Dispatcher.BeginInvoke(AttachToOpenPopup, DispatcherPriority.Loaded);
        }

        private void OnDropDownClosed(object? sender, EventArgs e)
        {
            DetachWheelHandler();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Detach();
        }

        private void AttachToOpenPopup()
        {
            DetachWheelHandler();

            _comboBox.ApplyTemplate();
            _popup = _comboBox.Template.FindName("PART_Popup", _comboBox) as Popup
                ?? FindDescendant<Popup>(_comboBox);

            var popupChild = _popup?.Child as UIElement;
            if (popupChild is null)
            {
                return;
            }

            _scrollViewer = FindDescendant<ScrollViewer>(popupChild);
            _handlerElement = _scrollViewer ?? popupChild;
            _handlerElement.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        private void DetachWheelHandler()
        {
            if (_handlerElement is not null)
            {
                _handlerElement.PreviewMouseWheel -= OnPreviewMouseWheel;
            }

            if (_scrollViewer is not null)
            {
                ScrollAnimationHelper.CancelVerticalAnimation(_scrollViewer);
            }

            _handlerElement = null;
            _scrollViewer = null;
            _popup = null;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            if (_scrollViewer is null)
            {
                e.Handled = true;
                return;
            }

            if (ModernScrollViewer.CanScrollVerticallyInWheelDirection(_scrollViewer, e.Delta))
            {
                ModernScrollViewer.ScrollVerticalWheel(
                    _scrollViewer,
                    e,
                    GetWheelMultiplier(_comboBox),
                    GetDuration(_comboBox),
                    useSmoothScrolling: true,
                    easingFunction: null);
            }

            e.Handled = true;
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
