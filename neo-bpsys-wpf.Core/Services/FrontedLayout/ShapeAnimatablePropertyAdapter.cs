using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class ShapeAnimatablePropertyAdapter : IAnimatablePropertyAdapter
{
    public bool CanHandle(FrontedAnimationTarget target, string propertyName) =>
        target.Element is Shape
        && AnimationAdapterHelpers.Is(propertyName, "FillColor", "StrokeColor", "StrokeThickness");

    public object? CaptureBaseValue(FrontedAnimationTarget target, string propertyName)
    {
        var shape = (Shape)target.Element;
        if (AnimationAdapterHelpers.Is(propertyName, "FillColor"))
        {
            return shape.Fill;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "StrokeColor"))
        {
            return shape.Stroke;
        }

        return shape.StrokeThickness;
    }

    public void SetValue(
        FrontedAnimationTarget target,
        string propertyName,
        string? value,
        FrontedAnimationExecutionContext context)
    {
        var shape = (Shape)target.Element;
        if (AnimationAdapterHelpers.Is(propertyName, "StrokeThickness"))
        {
            shape.StrokeThickness = AnimationAdapterHelpers.ParseDoubleOrDefault(value, shape.StrokeThickness);
            return;
        }

        if (!ColorHelper.TryParseColor(value, out var color))
        {
            context.Logger?.LogWarning("Invalid color value '{Value}' for {PropertyName}.", value, propertyName);
            return;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "FillColor"))
        {
            shape.Fill = new SolidColorBrush(color);
        }
        else
        {
            shape.Stroke = new SolidColorBrush(color);
        }
    }

    public Task AnimateAsync(
        FrontedAnimationTarget target,
        string propertyName,
        string? from,
        string? to,
        int durationMs,
        string? easing,
        FrontedAnimationExecutionContext context)
    {
        var shape = (Shape)target.Element;
        if (AnimationAdapterHelpers.Is(propertyName, "StrokeThickness"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                shape,
                Shape.StrokeThicknessProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, shape.StrokeThickness),
                durationMs,
                easing,
                context.CancellationToken);
        }

        if (!ColorHelper.TryParseColor(to, out var toColor))
        {
            context.Logger?.LogWarning("Invalid color value '{Value}' for {PropertyName}.", to, propertyName);
            return Task.CompletedTask;
        }

        var brush = EnsureSolidBrush(shape, propertyName);
        if (ColorHelper.TryParseColor(from, out var fromColor))
        {
            brush.Color = fromColor;
        }

        if (durationMs <= 0)
        {
            brush.Color = toColor;
            return Task.CompletedTask;
        }

        var animation = new ColorAnimation
        {
            To = toColor,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            FillBehavior = FillBehavior.HoldEnd,
            EasingFunction = AnimationAdapterHelpers.CreateEasing(easing)
        };
        return AnimationAdapterHelpers.BeginAnimationAsync(
            brush,
            SolidColorBrush.ColorProperty,
            animation,
            context.CancellationToken);
    }

    public void ResetValue(
        FrontedAnimationTarget target,
        string propertyName,
        object? baseValue,
        FrontedAnimationExecutionContext context)
    {
        var shape = (Shape)target.Element;
        if (AnimationAdapterHelpers.Is(propertyName, "FillColor"))
        {
            if (shape.Fill is SolidColorBrush fill)
            {
                fill.BeginAnimation(SolidColorBrush.ColorProperty, null);
            }

            shape.Fill = (Brush?)baseValue;
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "StrokeColor"))
        {
            if (shape.Stroke is SolidColorBrush stroke)
            {
                stroke.BeginAnimation(SolidColorBrush.ColorProperty, null);
            }

            shape.Stroke = (Brush?)baseValue;
        }
        else if (baseValue is double thickness)
        {
            shape.BeginAnimation(Shape.StrokeThicknessProperty, null);
            shape.StrokeThickness = thickness;
        }
    }

    private static SolidColorBrush EnsureSolidBrush(Shape shape, string propertyName)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "FillColor"))
        {
            if (shape.Fill is SolidColorBrush fill && !fill.IsFrozen)
            {
                return fill;
            }

            var brush = new SolidColorBrush(shape.Fill is SolidColorBrush existing ? existing.Color : Colors.Transparent);
            shape.Fill = brush;
            return brush;
        }

        if (shape.Stroke is SolidColorBrush stroke && !stroke.IsFrozen)
        {
            return stroke;
        }

        var next = new SolidColorBrush(shape.Stroke is SolidColorBrush existingStroke ? existingStroke.Color : Colors.Transparent);
        shape.Stroke = next;
        return next;
    }

    private static double? ParseNullableDouble(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : AnimationAdapterHelpers.ParseDoubleOrDefault(value);
}
