using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.PluginSdk;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 直接图片控件。
/// </summary>
[FrontedV3Control("Image", IsBuiltIn = true)]
public class ImageFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not ImageFrontedControlConfig imageConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not an Image config.");
        }

        var buildContext = context.ToBuildContext();
        var image = new Image();
        ImageFrontedControlLayoutHelper.ApplyImageSource(image, imageConfig, buildContext);
        ImageFrontedControlLayoutHelper.ApplyImageLayout(image, imageConfig, buildContext);
        var root = ImageFrontedControlLayoutHelper.CreateImageLayerRoot(
            context.ControlName ?? string.Empty,
            imageConfig,
            buildContext,
            image);
        Content = root;
    }
}
