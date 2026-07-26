using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 带外层边框容器的图片控件。
/// </summary>
[FrontedV3Control("BorderedImage", IsBuiltIn = true)]
public class BorderedImageFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not BorderedImageFrontedControlConfig imageConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a BorderedImage config.");
        }

        var buildContext = context.ToBuildContext();
        var border = FrontedControlFactoryHelper.CreateBorderWithoutCanvasLayout(context.ControlName ?? string.Empty);
        border.ClipToBounds = imageConfig.ClipToBounds;
        if (imageConfig.CornerRadius is > 0)
        {
            border.CornerRadius = new CornerRadius(imageConfig.CornerRadius.Value);
            ImageFrontedControlLayoutHelper.ApplyCornerRadiusClip(border, imageConfig.CornerRadius);
        }

        var image = new Image();
        if (imageConfig.ImageWidth.HasValue)
        {
            image.Width = imageConfig.ImageWidth.Value;
        }

        if (imageConfig.ImageHeight.HasValue)
        {
            image.Height = imageConfig.ImageHeight.Value;
        }

        ImageFrontedControlLayoutHelper.ApplyImageSource(image, imageConfig, buildContext);

        ImageFrontedControlLayoutHelper.ApplyImageLayout(image, imageConfig, buildContext);
        border.Child = ImageFrontedControlLayoutHelper.CreateBorderedImageContent(
            context.ControlName ?? string.Empty,
            imageConfig,
            buildContext,
            image);
        Content = border;
    }
}
