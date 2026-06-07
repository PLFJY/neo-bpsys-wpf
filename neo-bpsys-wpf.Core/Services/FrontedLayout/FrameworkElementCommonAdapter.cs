using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrameworkElementCommonAdapter : IAnimatablePropertyAdapter
{
    public bool CanHandle(FrontedAnimationTarget target, string propertyName) =>
        AnimationAdapterHelpers.Is(
            propertyName,
            "Opacity",
            "Visibility",
            "Width",
            "Height",
            "VisualOffsetX",
            "VisualOffsetY",
            "ScaleX",
            "ScaleY",
            "Rotation");

    public object? CaptureBaseValue(FrontedAnimationTarget target, string propertyName)
    {
        var element = target.Element;
        if (AnimationAdapterHelpers.Is(propertyName, "Opacity"))
        {
            return element.Opacity;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Visibility"))
        {
            return element.Visibility;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Width"))
        {
            return element.Width;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Height"))
        {
            return element.Height;
        }

        var transforms = EnsureTransforms(element);
        if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetX"))
        {
            return transforms.Translate.X;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetY"))
        {
            return transforms.Translate.Y;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "ScaleX"))
        {
            return transforms.Scale.ScaleX;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "ScaleY"))
        {
            return transforms.Scale.ScaleY;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Rotation"))
        {
            return transforms.Rotate.Angle;
        }

        return null;
    }

    public void SetValue(
        FrontedAnimationTarget target,
        string propertyName,
        string? value,
        FrontedAnimationExecutionContext context)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "Opacity"))
        {
            target.Element.Opacity = AnimationAdapterHelpers.ParseDoubleOrDefault(value, target.Element.Opacity);
            return;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Visibility"))
        {
            target.Element.Visibility = ParseVisibility(value);
            return;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Width"))
        {
            target.Element.Width = ParseLength(value, target.Element.Width);
            return;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Height"))
        {
            target.Element.Height = ParseLength(value, target.Element.Height);
            return;
        }

        var transforms = EnsureTransforms(target.Element);
        if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetX"))
        {
            transforms.Translate.X = AnimationAdapterHelpers.ParseDoubleOrDefault(value, transforms.Translate.X);
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetY"))
        {
            transforms.Translate.Y = AnimationAdapterHelpers.ParseDoubleOrDefault(value, transforms.Translate.Y);
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "ScaleX"))
        {
            transforms.Scale.ScaleX = AnimationAdapterHelpers.ParseDoubleOrDefault(value, transforms.Scale.ScaleX);
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "ScaleY"))
        {
            transforms.Scale.ScaleY = AnimationAdapterHelpers.ParseDoubleOrDefault(value, transforms.Scale.ScaleY);
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "Rotation"))
        {
            transforms.Rotate.Angle = AnimationAdapterHelpers.ParseDoubleOrDefault(value, transforms.Rotate.Angle);
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
        var element = target.Element;
        if (AnimationAdapterHelpers.Is(propertyName, "Visibility"))
        {
            SetValue(target, propertyName, to, context);
            return Task.CompletedTask;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Opacity"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                element,
                UIElement.OpacityProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, element.Opacity),
                durationMs,
                easing,
                context.CancellationToken);
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Width"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                element,
                FrameworkElement.WidthProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, double.IsNaN(element.Width) ? element.ActualWidth : element.Width),
                durationMs,
                easing,
                context.CancellationToken);
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Height"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                element,
                FrameworkElement.HeightProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, double.IsNaN(element.Height) ? element.ActualHeight : element.Height),
                durationMs,
                easing,
                context.CancellationToken);
        }

        var transforms = EnsureTransforms(element);
        if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetX"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                transforms.Translate,
                TranslateTransform.XProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, transforms.Translate.X),
                durationMs,
                easing,
                context.CancellationToken);
        }

        if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetY"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                transforms.Translate,
                TranslateTransform.YProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, transforms.Translate.Y),
                durationMs,
                easing,
                context.CancellationToken);
        }

        if (AnimationAdapterHelpers.Is(propertyName, "ScaleX"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                transforms.Scale,
                ScaleTransform.ScaleXProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, transforms.Scale.ScaleX),
                durationMs,
                easing,
                context.CancellationToken);
        }

        if (AnimationAdapterHelpers.Is(propertyName, "ScaleY"))
        {
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                transforms.Scale,
                ScaleTransform.ScaleYProperty,
                ParseNullableDouble(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, transforms.Scale.ScaleY),
                durationMs,
                easing,
                context.CancellationToken);
        }

        return AnimationAdapterHelpers.AnimateDoubleAsync(
            transforms.Rotate,
            RotateTransform.AngleProperty,
            ParseNullableDouble(from),
            AnimationAdapterHelpers.ParseDoubleOrDefault(to, transforms.Rotate.Angle),
            durationMs,
            easing,
            context.CancellationToken);
    }

    public void ResetValue(
        FrontedAnimationTarget target,
        string propertyName,
        object? baseValue,
        FrontedAnimationExecutionContext context)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "Opacity") && baseValue is double opacity)
        {
            target.Element.BeginAnimation(UIElement.OpacityProperty, null);
            target.Element.Opacity = opacity;
            return;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Visibility") && baseValue is Visibility visibility)
        {
            target.Element.Visibility = visibility;
            return;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Width") && baseValue is double width)
        {
            target.Element.BeginAnimation(FrameworkElement.WidthProperty, null);
            target.Element.Width = width;
            return;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "Height") && baseValue is double height)
        {
            target.Element.BeginAnimation(FrameworkElement.HeightProperty, null);
            target.Element.Height = height;
            return;
        }

        var transforms = EnsureTransforms(target.Element);
        if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetX") && baseValue is double x)
        {
            transforms.Translate.BeginAnimation(TranslateTransform.XProperty, null);
            transforms.Translate.X = x;
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "VisualOffsetY") && baseValue is double y)
        {
            transforms.Translate.BeginAnimation(TranslateTransform.YProperty, null);
            transforms.Translate.Y = y;
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "ScaleX") && baseValue is double scaleX)
        {
            transforms.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transforms.Scale.ScaleX = scaleX;
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "ScaleY") && baseValue is double scaleY)
        {
            transforms.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transforms.Scale.ScaleY = scaleY;
        }
        else if (AnimationAdapterHelpers.Is(propertyName, "Rotation") && baseValue is double angle)
        {
            transforms.Rotate.BeginAnimation(RotateTransform.AngleProperty, null);
            transforms.Rotate.Angle = angle;
        }
    }

    internal static FrontedPreviewTransformSet EnsureTransforms(FrameworkElement element)
    {
        if (element.RenderTransformOrigin == default)
        {
            element.RenderTransformOrigin = new Point(0.5D, 0.5D);
        }

        var group = element.RenderTransform as TransformGroup;
        if (group is null)
        {
            group = new TransformGroup();
            if (element.RenderTransform is not null and not MatrixTransform { Matrix.IsIdentity: true })
            {
                group.Children.Add(element.RenderTransform);
            }

            element.RenderTransform = group;
        }

        var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
        if (scale is null)
        {
            scale = new ScaleTransform(1D, 1D);
            group.Children.Add(scale);
        }

        var rotate = group.Children.OfType<RotateTransform>().FirstOrDefault();
        if (rotate is null)
        {
            rotate = new RotateTransform();
            group.Children.Add(rotate);
        }

        var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
        if (translate is null)
        {
            translate = new TranslateTransform();
            group.Children.Add(translate);
        }

        return new FrontedPreviewTransformSet(scale, rotate, translate);
    }

    private static Visibility ParseVisibility(string? value) =>
        Enum.TryParse<Visibility>(value, true, out var visibility) ? visibility : Visibility.Visible;

    private static double ParseLength(string? value, double fallback)
    {
        if (string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return double.NaN;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : fallback;
    }

    private static double? ParseNullableDouble(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : AnimationAdapterHelpers.ParseDoubleOrDefault(value);
}

internal sealed record FrontedPreviewTransformSet(
    ScaleTransform Scale,
    RotateTransform Rotate,
    TranslateTransform Translate);
