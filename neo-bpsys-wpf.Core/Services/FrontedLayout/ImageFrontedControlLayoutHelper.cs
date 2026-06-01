using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class ImageFrontedControlLayoutHelper
{
    public static void ApplyImageLayout(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        ApplyStretch(image, config, context);

        switch (config.SizingMode)
        {
            case ImageSizingMode.FillContainer:
                ApplyHorizontalAlignment(image, config, context, HorizontalAlignment.Stretch);
                ApplyVerticalAlignment(image, config, context, VerticalAlignment.Stretch);
                break;
            case ImageSizingMode.OverflowCrop:
                ApplyHorizontalAlignment(image, config, context, HorizontalAlignment.Center);
                ApplyVerticalAlignment(image, config, context, VerticalAlignment.Center);
                break;
            case ImageSizingMode.Auto:
            default:
                ApplyHorizontalAlignment(image, config, context);
                ApplyVerticalAlignment(image, config, context);
                break;
        }
    }

    public static void ApplyImageSource(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        if (!string.IsNullOrWhiteSpace(config.BindingPath))
        {
            BindingOperations.SetBinding(image, Image.SourceProperty, new Binding(config.BindingPath)
            {
                Source = context.SharedDataService
            });
            return;
        }

        if (!string.IsNullOrWhiteSpace(config.ImagePath))
        {
            image.Source = context.ResourceResolver.ResolveImage(
                config.ImagePath,
                FrontedImagePurpose.UiElement);
        }
    }

    public static void ApplyCornerRadiusClip(FrameworkElement element, double? cornerRadius)
    {
        if (!cornerRadius.HasValue || cornerRadius.Value <= 0)
        {
            return;
        }

        var radius = cornerRadius.Value;

        void UpdateClip()
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return;
            }

            element.Clip = new RectangleGeometry(
                new Rect(0, 0, element.ActualWidth, element.ActualHeight),
                radius,
                radius);
        }

        element.Loaded += (_, _) => UpdateClip();
        element.SizeChanged += (_, _) => UpdateClip();
    }

    private static void ApplyStretch(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        FrontedControlFactoryHelper.TryApplyEnum<Stretch>(
            config.Stretch,
            value => image.Stretch = value,
            context,
            nameof(config.Stretch));
    }

    private static void ApplyHorizontalAlignment(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context,
        HorizontalAlignment? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(config.HorizontalAlignment) && defaultValue is not null)
        {
            image.HorizontalAlignment = defaultValue.Value;
            return;
        }

        FrontedControlFactoryHelper.TryApplyEnum<HorizontalAlignment>(
            config.HorizontalAlignment,
            value => image.HorizontalAlignment = value,
            context,
            nameof(config.HorizontalAlignment));
    }

    private static void ApplyVerticalAlignment(
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context,
        VerticalAlignment? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(config.VerticalAlignment) && defaultValue is not null)
        {
            image.VerticalAlignment = defaultValue.Value;
            return;
        }

        FrontedControlFactoryHelper.TryApplyEnum<VerticalAlignment>(
            config.VerticalAlignment,
            value => image.VerticalAlignment = value,
            context,
            nameof(config.VerticalAlignment));
    }
}
