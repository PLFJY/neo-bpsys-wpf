using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class FrontedPseudoElementRenderer
{
    public static FrameworkElement Wrap(
        string controlName,
        FrontedControlConfigBase config,
        FrameworkElement content,
        FrontedControlBuildContext context)
    {
        if (config.PseudoElements.Count == 0)
        {
            return content;
        }

        var host = new Grid { Name = controlName, ClipToBounds = false };
        FrontedControlFactoryHelper.ApplyCanvasLayout(host, config);
        ResetCanvasLayout(content);

        var below = CreateLayer();
        var above = CreateLayer();
        host.Children.Add(below);
        host.Children.Add(content);
        host.Children.Add(above);

        foreach (var pseudoElement in config.PseudoElements)
        {
            if (string.IsNullOrWhiteSpace(pseudoElement.Name))
            {
                continue;
            }

            var element = CreateElement(pseudoElement, context);
            ApplyLayout(element, pseudoElement, host);
            MarkPart(element, host, controlName, config.BehaviorGuid, pseudoElement.Name);
            (pseudoElement.Layer == FrontedPseudoElementLayer.BelowContent ? below : above).Children.Add(element);
        }

        return host;
    }

    private static Canvas CreateLayer() =>
        new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };

    private static FrameworkElement CreateElement(
        FrontedPseudoElementConfig config,
        FrontedControlBuildContext context)
    {
        return config.Kind switch
        {
            FrontedPseudoElementKind.Border => new Border
            {
                Background = ParseBrush(config.Fill, context),
                BorderBrush = ParseBrush(config.Stroke, context),
                BorderThickness = new Thickness(Math.Max(0D, config.StrokeThickness))
            },
            FrontedPseudoElementKind.Image => new Image
            {
                Source = context.ResourceResolver.ResolveImage(config.ImagePath, FrontedImagePurpose.UiElement),
                Stretch = Stretch.Fill
            },
            _ => new Rectangle
            {
                Fill = ParseBrush(config.Fill, context),
                Stroke = ParseBrush(config.Stroke, context),
                StrokeThickness = Math.Max(0D, config.StrokeThickness)
            }
        };
    }

    private static void ApplyLayout(
        FrameworkElement element,
        FrontedPseudoElementConfig config,
        FrameworkElement parent)
    {
        Canvas.SetLeft(element, config.Left);
        Canvas.SetTop(element, config.Top);
        Panel.SetZIndex(element, config.ZIndex);
        element.Opacity = Math.Clamp(config.Opacity, 0D, 1D);
        element.Visibility = Enum.TryParse<Visibility>(config.Visibility, true, out var visibility)
            ? visibility
            : Visibility.Hidden;
        element.IsHitTestVisible = config.IsHitTestVisible;

        void UpdateSize()
        {
            element.Width = ResolveLength(config.WidthText, config.Width, parent.ActualWidth, element.Width);
            element.Height = ResolveLength(config.HeightText, config.Height, parent.ActualHeight, element.Height);
        }

        parent.Loaded += (_, _) => UpdateSize();
        parent.SizeChanged += (_, _) => UpdateSize();
        UpdateSize();
    }

    private static double ResolveLength(string? text, double? pixels, double parentSize, double fallback)
    {
        if (!string.IsNullOrWhiteSpace(text)
            && text.Trim().EndsWith('%')
            && double.TryParse(
                text.Trim()[..^1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var percentage))
        {
            return Math.Max(0D, parentSize * percentage / 100D);
        }

        if (!string.IsNullOrWhiteSpace(text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var textPixels))
        {
            return Math.Max(0D, textPixels);
        }

        return pixels is { } fixedSize && double.IsFinite(fixedSize)
            ? Math.Max(0D, fixedSize)
            : fallback;
    }

    private static Brush? ParseBrush(string? value, FrontedControlBuildContext context)
    {
        Brush? brush = null;
        FrontedControlFactoryHelper.TryApplyTypeConverter<Brush>(
            value,
            parsed => brush = parsed,
            context,
            nameof(FrontedPseudoElementConfig.Fill));
        return brush;
    }

    private static void MarkPart(
        FrameworkElement element,
        FrameworkElement parent,
        string parentName,
        Guid parentGuid,
        string partName)
    {
        FrontedRendererProperties.SetIsGeneratedControl(element, true);
        FrontedRendererProperties.SetIsAnimationAuxiliaryElement(element, true);
        FrontedRendererProperties.SetParentBehaviorGuid(element, parentGuid);
        FrontedRendererProperties.SetParentRegisteredName(element, parentName);
        FrontedRendererProperties.SetAnimationPartName(element, partName);
        FrontedRendererProperties.SetAnimationPartParent(element, parent);
        FrontedRendererProperties.SetRegisteredName(element, $"{parentName}__{partName}");
    }

    private static void ResetCanvasLayout(FrameworkElement content)
    {
        content.Name = string.Empty;
        content.ClearValue(Canvas.LeftProperty);
        content.ClearValue(Canvas.TopProperty);
        content.ClearValue(Panel.ZIndexProperty);
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.VerticalAlignment = VerticalAlignment.Stretch;
    }
}
