using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;

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

        var image = new Image();
        ImageFrontedControlLayoutHelper.ApplyImageSource(image, imageConfig, context);
        ImageFrontedControlLayoutHelper.ApplyImageLayout(image, imageConfig, context);
        return ImageFrontedControlLayoutHelper.CreateImageLayerRoot(name, imageConfig, context, image);
    }
}
