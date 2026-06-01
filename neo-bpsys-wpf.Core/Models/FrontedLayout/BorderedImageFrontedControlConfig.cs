namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 带外层边框容器的图片控件配置。
/// </summary>
public class BorderedImageFrontedControlConfig : ImageFrontedControlConfig
{
    /// <summary>
    /// 初始化带外层边框容器的图片控件配置。
    /// </summary>
    public BorderedImageFrontedControlConfig()
    {
        ControlType = "BorderedImage";
    }

    /// <summary>
    /// 内层 Image 的显式宽度。为空时由外层容器布局槽决定。
    /// </summary>
    public double? ImageWidth { get; set; }

    /// <summary>
    /// 内层 Image 的显式高度。为空时由外层容器布局槽决定。
    /// </summary>
    public double? ImageHeight { get; set; }
}
