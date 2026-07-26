using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 在前台元素上存储支持动画的裁剪内边距值。
/// </summary>
public static class FrontedClipInsetProperties
{
    /// <summary>
    /// 距左边缘的裁剪内边距。
    /// </summary>
    public static readonly DependencyProperty LeftProperty = Register("Left");

    /// <summary>
    /// 距上边缘的裁剪内边距。
    /// </summary>
    public static readonly DependencyProperty TopProperty = Register("Top");

    /// <summary>
    /// 距右边缘的裁剪内边距。
    /// </summary>
    public static readonly DependencyProperty RightProperty = Register("Right");

    /// <summary>
    /// 距下边缘的裁剪内边距。
    /// </summary>
    public static readonly DependencyProperty BottomProperty = Register("Bottom");

    private static DependencyProperty Register(string name) =>
        DependencyProperty.RegisterAttached(
            name,
            typeof(double),
            typeof(FrontedClipInsetProperties),
            new PropertyMetadata(0D, OnInsetChanged));

    private static void OnInsetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.SizeChanged -= OnElementSizeChanged;
        element.SizeChanged += OnElementSizeChanged;
        UpdateClip(element);
    }

    private static void OnElementSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            UpdateClip(element);
        }
    }

    private static void UpdateClip(FrameworkElement element)
    {
        var width = ResolveSize(element.Width, element.ActualWidth);
        var height = ResolveSize(element.Height, element.ActualHeight);
        var left = Math.Clamp((double)element.GetValue(LeftProperty), 0D, width);
        var top = Math.Clamp((double)element.GetValue(TopProperty), 0D, height);
        var right = Math.Clamp((double)element.GetValue(RightProperty), 0D, Math.Max(0D, width - left));
        var bottom = Math.Clamp((double)element.GetValue(BottomProperty), 0D, Math.Max(0D, height - top));
        element.Clip = new RectangleGeometry(new Rect(
            left,
            top,
            Math.Max(0D, width - left - right),
            Math.Max(0D, height - top - bottom)));
    }

    private static double ResolveSize(double configured, double actual) =>
        configured > 0D && double.IsFinite(configured)
            ? configured
            : actual > 0D && double.IsFinite(actual) ? actual : 0D;
}
