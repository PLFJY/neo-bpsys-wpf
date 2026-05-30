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

        FrontedControlFactoryHelper.TryApplyEnum<Stretch>(
            imageConfig.Stretch,
            value => image.Stretch = value,
            context,
            nameof(imageConfig.Stretch));
        FrontedControlFactoryHelper.TryApplyEnum<HorizontalAlignment>(
            imageConfig.HorizontalAlignment,
            value => image.HorizontalAlignment = value,
            context,
            nameof(imageConfig.HorizontalAlignment));
        FrontedControlFactoryHelper.TryApplyEnum<VerticalAlignment>(
            imageConfig.VerticalAlignment,
            value => image.VerticalAlignment = value,
            context,
            nameof(imageConfig.VerticalAlignment));

        border.Child = image;
        return border;
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
