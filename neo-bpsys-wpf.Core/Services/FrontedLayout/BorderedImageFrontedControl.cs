using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 带外层边框容器的图片控件工厂。
/// </summary>
public class BorderedImageFrontedControl : IFrontedControl
{
    /// <inheritdoc />
    public string ControlType => "BorderedImage";

    /// <inheritdoc />
    public Type ConfigType => typeof(BorderedImageFrontedControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not BorderedImageFrontedControlConfig imageConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a BorderedImage config.");
        }

        var border = FrontedControlFactoryHelper.CreateOuterBorder(name, imageConfig);
        border.ClipToBounds = imageConfig.ClipToBounds || imageConfig.SizingMode != ImageSizingMode.Auto;
        if (imageConfig.CornerRadius is > 0)
        {
            border.CornerRadius = new CornerRadius(imageConfig.CornerRadius.Value);
        }

        var host = new Grid
        {
            ClipToBounds = imageConfig.ClipToBounds || imageConfig.SizingMode != ImageSizingMode.Auto
        };
        ImageFrontedControlLayoutHelper.ApplyCornerRadiusClip(host, imageConfig.CornerRadius);

        var image = new Image();
        if (imageConfig.ImageWidth.HasValue)
        {
            image.Width = imageConfig.ImageWidth.Value;
        }

        if (imageConfig.ImageHeight.HasValue)
        {
            image.Height = imageConfig.ImageHeight.Value;
        }

        if (!string.IsNullOrWhiteSpace(imageConfig.BindingPath))
        {
            BindingOperations.SetBinding(image, Image.SourceProperty, new Binding(imageConfig.BindingPath)
            {
                Source = context.SharedDataService
            });
        }

        ImageFrontedControlLayoutHelper.ApplyImageLayout(image, imageConfig, context);
        host.Children.Add(image);
        border.Child = host;
        return border;
    }
}
