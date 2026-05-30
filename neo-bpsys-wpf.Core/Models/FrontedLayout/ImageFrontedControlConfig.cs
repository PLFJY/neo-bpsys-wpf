namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 图片控件配置。
/// </summary>
public class ImageFrontedControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化图片控件配置。
    /// </summary>
    public ImageFrontedControlConfig()
    {
        ControlType = "Image";
    }

    /// <summary>
    /// 图片拉伸方式。
    /// </summary>
    public string? Stretch { get; set; }

    /// <summary>
    /// 图片圆角半径。
    /// </summary>
    public double? CornerRadius { get; set; }

    /// <summary>
    /// 是否声明可创建选择边框覆盖层。
    /// </summary>
    public bool PickingBorder { get; set; }

    /// <summary>
    /// 选择边框图片路径。
    /// </summary>
    public string? PickingBorderImagePath { get; set; }

    /// <summary>
    /// 是否声明可创建 Ban 锁覆盖层。
    /// </summary>
    public bool BanLockAvailable { get; set; }

    /// <summary>
    /// Ban 锁图片路径。
    /// </summary>
    public string? BanLockImagePath { get; set; }
}
