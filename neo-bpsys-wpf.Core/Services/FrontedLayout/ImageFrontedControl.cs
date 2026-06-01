using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using UiImage = Wpf.Ui.Controls.Image;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 直接图片控件工厂。
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

        var image = new UiImage { Name = name };
        FrontedControlFactoryHelper.ApplyCanvasLayout(image, imageConfig);
        image.ClipToBounds = imageConfig.ClipToBounds;
        if (imageConfig.CornerRadius is > 0)
        {
            image.CornerRadius = new CornerRadius(imageConfig.CornerRadius.Value);
        }

        if (!string.IsNullOrWhiteSpace(imageConfig.BindingPath))
        {
            BindingOperations.SetBinding(image, UiImage.SourceProperty, new Binding(imageConfig.BindingPath)
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
        return image;
    }
}
