using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 当前局/全局 Ban 位显示控件配置。
/// </summary>
public class BanSlotDisplayControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化 Ban 位显示控件配置。
    /// </summary>
    public BanSlotDisplayControlConfig()
    {
        ControlType = "BanSlotDisplay";
    }

    /// <summary>
    /// Ban 位来源。
    /// </summary>
    public BanSlotKind SlotKind { get; set; }

    /// <summary>
    /// Ban 位阵营。
    /// </summary>
    public Camp Camp { get; set; }

    /// <summary>
    /// Ban 位索引。
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 是否显示锁定覆盖层。
    /// </summary>
    public bool ShowLockOverlay { get; set; } = true;

    /// <summary>
    /// 图片拉伸方式。
    /// </summary>
    public string? Stretch { get; set; }

    /// <summary>
    /// 图片水平对齐。
    /// </summary>
    public string? HorizontalAlignment { get; set; }

    /// <summary>
    /// 图片垂直对齐。
    /// </summary>
    public string? VerticalAlignment { get; set; }

    /// <summary>
    /// 图片尺寸模式。
    /// </summary>
    public ImageSizingMode SizingMode { get; set; } = ImageSizingMode.Auto;

    /// <summary>
    /// 锁定覆盖层图片路径。为空时使用 BpWindowSettings 默认锁图。
    /// </summary>
    public string? LockImageSource { get; set; }

    /// <summary>
    /// 锁定覆盖层相对控件的 ZIndex 偏移。
    /// </summary>
    public int LockZIndexOffset { get; set; } = 1;
}
