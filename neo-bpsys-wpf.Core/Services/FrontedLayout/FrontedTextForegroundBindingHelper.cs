using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Applies static or shared-data-bound foreground colors to Designer v3 text elements.
/// </summary>
public static class FrontedTextForegroundBindingHelper
{
    /// <summary>
    /// Applies foreground color to a text block, using <paramref name="colorBindingPath"/> when it has a value.
    /// </summary>
    /// <param name="textBlock">The text block to style.</param>
    /// <param name="staticColor">The static foreground color used when no binding path is set.</param>
    /// <param name="colorBindingPath">The shared data binding path that supplies a color string.</param>
    /// <param name="context">The fronted control build context.</param>
    /// <param name="propertyName">The static color property name used in diagnostics.</param>
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
    /// Applies foreground color to a text block, using <paramref name="colorBindingPath"/> when it has a value.
    /// </summary>
    /// <param name="textBlock">The text block to style.</param>
    /// <param name="staticColor">The static foreground color used when no binding path is set.</param>
    /// <param name="colorBindingPath">The shared data binding path that supplies a color string.</param>
    /// <param name="sharedDataService">The source object for shared data binding.</param>
    /// <param name="logger">Optional logger for invalid color diagnostics.</param>
    /// <param name="propertyName">The static color property name used in diagnostics.</param>
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
