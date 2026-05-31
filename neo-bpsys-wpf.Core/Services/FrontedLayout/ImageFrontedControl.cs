using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 图片控件工厂。
/// </summary>
public class ImageFrontedControl : IFrontedControl
{
    /// <inheritdoc />
    public string ControlType => "Image";

    /// <inheritdoc />
    public Type ConfigType => typeof(ImageFrontedControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not ImageFrontedControlConfig imageConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not an Image config.");
        }

        var border = FrontedControlFactoryHelper.CreateOuterBorder(name, imageConfig);
        var image = new Image();
        border.ClipToBounds = imageConfig.ClipToBounds;
        ApplyCornerRadius(border, image, imageConfig.CornerRadius);

        if (!string.IsNullOrWhiteSpace(imageConfig.BindingPath))
        {
            BindingOperations.SetBinding(image, Image.SourceProperty, new Binding(imageConfig.BindingPath)
            {
                Source = context.SharedDataService
            });
        }

        ApplyImageLayout(border, image, imageConfig, context);

        border.Child = image;
        return border;
    }

    private static void ApplyImageLayout(
        Border border,
        Image image,
        ImageFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        ApplyStretch(image, config, context);

        switch (config.SizingMode)
        {
            case ImageSizingMode.FillContainer:
                BindImageToBorderSize(border, image);
                ApplyHorizontalAlignment(
                    image,
                    config,
                    context,
                    HorizontalAlignment.Stretch);
                ApplyVerticalAlignment(
                    image,
                    config,
                    context,
                    VerticalAlignment.Stretch);
                break;
            case ImageSizingMode.OverflowCrop:
                ApplyHorizontalAlignment(
                    image,
                    config,
                    context,
                    HorizontalAlignment.Center);
                ApplyVerticalAlignment(
                    image,
                    config,
                    context,
                    VerticalAlignment.Center);
                break;
            case ImageSizingMode.Auto:
            default:
                ApplyHorizontalAlignment(image, config, context);
                ApplyVerticalAlignment(image, config, context);
                break;
        }
    }

    private static void BindImageToBorderSize(Border border, Image image)
    {
        BindingOperations.SetBinding(image, FrameworkElement.WidthProperty, new Binding(nameof(Border.ActualWidth))
        {
            Source = border
        });
        BindingOperations.SetBinding(image, FrameworkElement.HeightProperty, new Binding(nameof(Border.ActualHeight))
        {
            Source = border
        });
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

    private static void ApplyCornerRadius(Border border, Image image, double? cornerRadius)
    {
        if (!cornerRadius.HasValue || cornerRadius.Value <= 0)
        {
            return;
        }

        var radius = cornerRadius.Value;
        border.CornerRadius = new CornerRadius(radius);
        border.ClipToBounds = true;

        void UpdateClip()
        {
            if (image.ActualWidth <= 0 || image.ActualHeight <= 0)
            {
                return;
            }

            image.Clip = new RectangleGeometry(
                new Rect(0, 0, image.ActualWidth, image.ActualHeight),
                radius,
                radius);
        }

        image.SizeChanged += (_, _) => UpdateClip();
    }
}
