using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 为 ColorPicker 内部的颜色条注册点击即拖动样式。
/// </summary>
/// <remarks>
/// ColorPicker 的 HSV/HSL/RGB 颜色条类型是程序集内部类型，无法在 XAML 中直接作为
/// <see cref="Style.TargetType"/> 引用，因此启动时按实际 Slider 类型注册隐式样式。
/// </remarks>
internal static class ColorPickerSliderStyleInitializer
{
    private static int _initialized;
    private static int _classHandlerRegistered;

    /// <summary>
    /// 确保 ColorPicker 颜色条的隐式样式已经注册；重复调用无副作用。
    /// </summary>
    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        // 不依赖样式是否命中：ColorPicker 的内部 Slider 子类统一在类级别兜底。
        EnsureClassHandler();

        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var colorPickerAssembly = typeof(ColorPicker.PortableColorPicker).Assembly;
        foreach (var type in GetSliderTypes(colorPickerAssembly))
        {
            var baseStyle = application.TryFindResource(type) as Style;
            var style = new Style(type, baseStyle);
            style.Setters.Add(new Setter(Slider.IsMoveToPointEnabledProperty, true));
            style.Setters.Add(new Setter(ColorPickerSliderDragBehavior.IsEnabledProperty, true));
            application.Resources[type] = style;
        }
    }

    private static void EnsureClassHandler()
    {
        if (Interlocked.Exchange(ref _classHandlerRegistered, 1) != 0)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(Slider),
            UIElement.PreviewMouseDownEvent,
            new MouseButtonEventHandler(OnSliderPreviewMouseDown),
            handledEventsToo: true);
    }

    private static void OnSliderPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider slider &&
            slider.GetType().Assembly == typeof(ColorPicker.PortableColorPicker).Assembly)
        {
            ColorPickerSliderDragBehavior.HandlePreviewMouseLeftButtonDown(sender, e);
        }
    }

    private static IEnumerable<Type> GetSliderTypes(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsAbstract && typeof(Slider).IsAssignableFrom(type))
            {
                yield return type;
            }
        }
    }
}
