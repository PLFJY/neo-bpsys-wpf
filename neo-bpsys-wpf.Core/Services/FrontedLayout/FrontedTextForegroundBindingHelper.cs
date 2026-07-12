using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将静态或共享数据绑定的前景色应用于设计器 v3 文本元素。
/// </summary>
public static class FrontedTextForegroundBindingHelper
{
    /// <summary>
    /// 将前景色应用于文本块，当 <paramref name="colorBindingPath"/> 有值时使用它。
    /// </summary>
    /// <param name="textBlock">要设置样式的文本块。</param>
    /// <param name="staticColor">未设置绑定路径时使用的静态前景色。</param>
    /// <param name="colorBindingPath">提供颜色字符串的共享数据绑定路径。</param>
    /// <param name="context">前台控件构建上下文。</param>
    /// <param name="propertyName">诊断中使用的静态颜色属性名称。</param>
    public static void ApplyForeground(
        TextBlock textBlock,
        string? staticColor,
        string? colorBindingPath,
        FrontedControlBuildContext context,
        string propertyName)
    {
        ApplyForeground(
            textBlock,
            staticColor,
            colorBindingPath,
            context.SharedDataService,
            context.Logger,
            propertyName);
    }

    /// <summary>
    /// 将前景色应用于文本块，当 <paramref name="colorBindingPath"/> 有值时使用它。
    /// </summary>
    /// <param name="textBlock">要设置样式的文本块。</param>
    /// <param name="staticColor">未设置绑定路径时使用的静态前景色。</param>
    /// <param name="colorBindingPath">提供颜色字符串的共享数据绑定路径。</param>
    /// <param name="sharedDataService">共享数据绑定的源对象。</param>
    /// <param name="logger">用于无效颜色诊断的可选日志记录器。</param>
    /// <param name="propertyName">诊断中使用的静态颜色属性名称。</param>
    public static void ApplyForeground(
        TextBlock textBlock,
        string? staticColor,
        string? colorBindingPath,
        ISharedDataService sharedDataService,
        ILogger? logger,
        string propertyName)
    {
        if (!string.IsNullOrWhiteSpace(colorBindingPath))
        {
            var fallback = CreateBrush(staticColor, Colors.White, logger, propertyName);
            BindingOperations.SetBinding(textBlock, TextBlock.ForegroundProperty, new Binding(colorBindingPath)
            {
                Source = sharedDataService,
                Mode = BindingMode.OneWay,
                Converter = new HexToBrushConverter(logger),
                ConverterParameter = fallback,
                FallbackValue = fallback,
                TargetNullValue = fallback
            });
            return;
        }

        if (!string.IsNullOrWhiteSpace(staticColor))
        {
            textBlock.Foreground = CreateBrush(staticColor, Colors.White, logger, propertyName);
        }
    }

    private static SolidColorBrush CreateBrush(
        string? value,
        Color fallback,
        ILogger? logger,
        string propertyName)
    {
        if (ColorHelper.TryParseColor(value, out var color))
        {
            return new SolidColorBrush(color);
        }

        logger?.LogWarning(
            "Invalid text color. Property: {PropertyName}, Value: {Value}",
            propertyName,
            value);
        return new SolidColorBrush(fallback);
    }

    private sealed class HexToBrushConverter(ILogger? logger) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (ColorHelper.TryParseColor(value as string, out var color))
            {
                return new SolidColorBrush(color);
            }

            logger?.LogWarning("Invalid bound text foreground color: {Value}", value);
            return parameter is Brush fallback ? fallback : new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
