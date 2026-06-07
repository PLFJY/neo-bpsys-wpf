using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class ShapeFillBrushFactory
{
    public static void Apply(
        Shape shape,
        ShapeFrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        ApplyStroke(shape, config, context);
        if (config.FillMode == ShapeFillMode.LinearGradient || config.UseGradient)
        {
            shape.Fill = CreateGradient(config, context);
            return;
        }

        // Binding is active when FillBindingPath has a value (no separate toggle)
        if (!string.IsNullOrWhiteSpace(config.FillBindingPath))
        {
            var fallback = CreateBrush(config.FillColor, Colors.White, context, nameof(config.FillColor));
            BindingOperations.SetBinding(shape, Shape.FillProperty, CreateBinding(
                config.FillBindingPath,
                context,
                new HexToBrushConverter(context.Logger),
                fallback));
            return;
        }

        shape.Fill = CreateBrush(config.FillColor, Colors.White, context, nameof(config.FillColor));
    }

    private static LinearGradientBrush CreateGradient(
        ShapeFrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        var (startPoint, endPoint) = ShapeGradientAngleHelper.ToRelativePoints(config.GradientAngle);
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            StartPoint = startPoint,
            EndPoint = endPoint
        };

        var start = new GradientStop(
            ParseColor(config.FillColor, Colors.White, context, nameof(config.FillColor)),
            0);
        var end = new GradientStop(
            ParseColor(config.GradientEndColor, Colors.Transparent, context, nameof(config.GradientEndColor)),
            1);
        brush.GradientStops.Add(start);
        brush.GradientStops.Add(end);

        // Gradient start binding: use FillBindingPath (unified with solid fill), fallback to deprecated GradientStartBindingPath
        var startBindingPath = !string.IsNullOrWhiteSpace(config.FillBindingPath)
            ? config.FillBindingPath
            : config.GradientStartBindingPath;
        if (!string.IsNullOrWhiteSpace(startBindingPath))
        {
            BindingOperations.SetBinding(start, GradientStop.ColorProperty, CreateBinding(
                startBindingPath,
                context,
                new HexToColorConverter(context.Logger),
                start.Color));
        }

        // Gradient end binding: active when path has a value (no separate toggle)
        if (!string.IsNullOrWhiteSpace(config.GradientEndBindingPath))
        {
            BindingOperations.SetBinding(end, GradientStop.ColorProperty, CreateBinding(
                config.GradientEndBindingPath,
                context,
                new HexToColorConverter(context.Logger),
                end.Color));
        }

        return brush;
    }

    private static void ApplyStroke(
        Shape shape,
        ShapeFrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        shape.StrokeThickness = double.IsFinite(config.StrokeThickness)
            ? Math.Max(0, config.StrokeThickness)
            : 0;
        if (!string.IsNullOrWhiteSpace(config.StrokeColor))
        {
            shape.Stroke = CreateBrush(config.StrokeColor, Colors.Transparent, context, nameof(config.StrokeColor));
        }
    }

    private static Binding CreateBinding(
        string path,
        FrontedControlBuildContext context,
        IValueConverter converter,
        object fallbackValue) =>
        new(path)
        {
            Source = context.SharedDataService,
            Mode = BindingMode.OneWay,
            Converter = converter,
            FallbackValue = fallbackValue,
            TargetNullValue = fallbackValue
        };

    private static SolidColorBrush CreateBrush(
        string? value,
        Color fallback,
        FrontedControlBuildContext context,
        string propertyName) =>
        new(ParseColor(value, fallback, context, propertyName));

    private static Color ParseColor(
        string? value,
        Color fallback,
        FrontedControlBuildContext context,
        string propertyName)
    {
        if (ColorHelper.TryParseColor(value, out var color))
        {
            return color;
        }

        context.Logger?.LogWarning(
            "Invalid shape color. Property: {PropertyName}, Value: {Value}",
            propertyName,
            value);
        return fallback;
    }

    private sealed class HexToBrushConverter(ILogger? logger) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (ColorHelper.TryParseColor(value as string, out var color))
            {
                return new SolidColorBrush(color);
            }

            logger?.LogWarning("Invalid bound shape fill color: {Value}", value);
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }

    private sealed class HexToColorConverter(ILogger? logger) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (ColorHelper.TryParseColor(value as string, out var color))
            {
                return color;
            }

            logger?.LogWarning("Invalid bound shape gradient color: {Value}", value);
            return Colors.White;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
