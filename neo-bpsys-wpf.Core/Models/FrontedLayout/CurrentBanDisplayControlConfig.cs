using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 当前局 Ban 位显示控件配置。
/// </summary>
public class CurrentBanDisplayControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化当前局 Ban 位显示控件配置。
    /// </summary>
    public CurrentBanDisplayControlConfig()
    {
        ControlType = "CurrentBanDisplay";
    }

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
}
