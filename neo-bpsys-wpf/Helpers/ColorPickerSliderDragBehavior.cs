using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 让 ColorPicker 的颜色条支持“点击轨道即开始拖动”。
/// </summary>
/// <remarks>
/// WPF Slider 的 <see cref="Slider.IsMoveToPointEnabled"/> 只会在点击轨道时移动一次滑块，
/// 不会接管后续鼠标移动；这里在点击非滑块区域时主动设置值并监听所属视觉根上的鼠标事件，
/// 后续移动直接按轨道位置更新值，避免用户必须再次点击滑块才能拖动。
/// </remarks>
internal static class ColorPickerSliderDragBehavior
{
    private sealed class DragState
    {
        public bool HandlersAttached;
        public bool IsDragging;
        public bool SawPressedMove;
        public int LastMouseDownTimestamp;
        public Track? Track;
        public UIElement? Root;
        public MouseEventHandler? RootMouseMoveHandler;
        public MouseButtonEventHandler? RootMouseUpHandler;
        public MouseEventHandler? RootLostCaptureHandler;
    }

    private static readonly ConditionalWeakTable<Slider, DragState> States = new();

    /// <summary>
    /// 标识是否启用点击轨道即拖动的附加属性。
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ColorPickerSliderDragBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// 获取颜色条是否启用点击轨道即拖动。
    /// </summary>
    /// <param name="element">目标元素。</param>
    /// <returns>是否启用。</returns>
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// 设置颜色条是否启用点击轨道即拖动。
    /// </summary>
    /// <param name="element">目标元素。</param>
    /// <param name="value">是否启用。</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider || e.NewValue is not true)
        {
            return;
        }

        var state = States.GetOrCreateValue(slider);
        if (state.HandlersAttached)
        {
            return;
        }

        state.HandlersAttached = true;
        // handledEventsToo 保证即使 ColorPicker 内部先处理了预览事件，这里仍能接管轨道点击。
        slider.AddHandler(
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(HandlePreviewMouseLeftButtonDown),
            handledEventsToo: true);
    }

    internal static void HandlePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        // 类处理器与样式附加处理器会因同一个 MouseDown 同时到达，必须按事件时间戳去重；
        // 但不能因为上一次会话在弹层外释放而永久拒绝后续点击，否则颜色条会彻底失去响应。
        if (States.TryGetValue(slider, out var activeState) && activeState.IsDragging)
        {
            if (activeState.LastMouseDownTimestamp == e.Timestamp)
            {
                return;
            }

            EndDrag(slider, activeState);
        }

        // 点在滑块本身上时保留 WPF 原生拖动逻辑。
        if (IsThumbSource(e.OriginalSource as DependencyObject, slider))
        {
            return;
        }

        var track = FindTrack(slider);
        if (track is null || track.ActualWidth <= 0D || track.ActualHeight <= 0D)
        {
            return;
        }

        var value = track.ValueFromPoint(e.GetPosition(track));
        if (!double.IsFinite(value))
        {
            return;
        }

        var state = States.GetOrCreateValue(slider);
        slider.SetCurrentValue(RangeBase.ValueProperty, value);
        state.Track = track;
        state.SawPressedMove = false;
        state.IsDragging = true;
        state.LastMouseDownTimestamp = e.Timestamp;
        AttachRootHandlers(slider, state);
        // 不主动捕获鼠标：ColorPicker 的 Popup 依赖自身捕获保持展开，
        // 抢走捕获会关闭弹层并导致拖动失效。后续移动由视觉根上的预览事件继续处理。
        e.Handled = true;
    }

    private static void AttachRootHandlers(Slider slider, DragState state)
    {
        // ColorPicker 的弹出层可能位于独立的 PopupRoot，因此监听实际视觉根而不是外层 Window。
        var root = PresentationSource.FromVisual(slider)?.RootVisual as UIElement
                   ?? Window.GetWindow(slider);
        if (root is null || ReferenceEquals(root, slider))
        {
            return;
        }

        state.Root = root;
        state.RootMouseMoveHandler = (_, e) => Root_OnMouseMove(slider, state, e);
        state.RootMouseUpHandler = (_, e) => Root_OnMouseLeftButtonUp(slider, state, e);
        state.RootLostCaptureHandler = (_, e) => Root_OnLostMouseCapture(slider, state, e);
        root.AddHandler(UIElement.PreviewMouseMoveEvent, state.RootMouseMoveHandler, handledEventsToo: true);
        root.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, state.RootMouseUpHandler, handledEventsToo: true);
        root.AddHandler(UIElement.LostMouseCaptureEvent, state.RootLostCaptureHandler, handledEventsToo: true);
    }

    private static void Root_OnMouseMove(Slider slider, DragState state, MouseEventArgs e)
    {
        if (!state.IsDragging)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            state.SawPressedMove = true;
        }
        else if (state.SawPressedMove)
        {
            // 鼠标在根元素外释放时可能收不到 MouseUp；下一次移动按已释放处理，避免继续改值。
            EndDrag(slider, state);
            return;
        }

        var track = state.Track ?? FindTrack(slider);
        if (track is null)
        {
            EndDrag(slider, state);
            return;
        }

        var value = track.ValueFromPoint(e.GetPosition(track));
        if (double.IsFinite(value))
        {
            slider.SetCurrentValue(RangeBase.ValueProperty, value);
        }

        e.Handled = true;
    }

    private static void Root_OnMouseLeftButtonUp(Slider slider, DragState state, MouseButtonEventArgs e)
    {
        if (!state.IsDragging)
        {
            return;
        }

        if (state.Track is { } track)
        {
            var value = track.ValueFromPoint(e.GetPosition(track));
            if (double.IsFinite(value))
            {
                slider.SetCurrentValue(RangeBase.ValueProperty, value);
            }
        }

        EndDrag(slider, state);
        e.Handled = true;
    }

    private static void Root_OnLostMouseCapture(Slider slider, DragState state, MouseEventArgs e)
    {
        // LostMouseCapture 会从失去捕获的子元素冒泡到根；只处理滑杆自身丢失捕获，
        // 避免点击轨道后 Thumb 失去捕获导致拖动刚开始就被结束。
        if (state.IsDragging && ReferenceEquals(e.OriginalSource, slider))
        {
            EndDrag(slider, state);
        }
    }

    private static void EndDrag(Slider slider, DragState state)
    {
        state.IsDragging = false;
        state.SawPressedMove = false;
        state.LastMouseDownTimestamp = 0;
        state.Track = null;

        if (state.Root is { } root)
        {
            if (state.RootMouseMoveHandler is { } mouseMoveHandler)
            {
                root.RemoveHandler(UIElement.PreviewMouseMoveEvent, mouseMoveHandler);
            }

            if (state.RootMouseUpHandler is { } mouseUpHandler)
            {
                root.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, mouseUpHandler);
            }

            if (state.RootLostCaptureHandler is { } lostCaptureHandler)
            {
                root.RemoveHandler(UIElement.LostMouseCaptureEvent, lostCaptureHandler);
            }
        }

        state.Root = null;
        state.RootMouseMoveHandler = null;
        state.RootMouseUpHandler = null;
        state.RootLostCaptureHandler = null;
        if (slider.IsMouseCaptured)
        {
            slider.ReleaseMouseCapture();
        }
    }

    private static bool IsThumbSource(DependencyObject? source, Slider slider)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, slider);)
        {
            if (current is Thumb)
            {
                return true;
            }

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private static Track? FindTrack(Slider slider)
    {
        if (slider.Template?.FindName("PART_Track", slider) is Track templateTrack)
        {
            return templateTrack;
        }

        return FindVisualChild<Track>(slider);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualChild<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
