namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 BP 选择呼吸边框覆盖控件配置。
/// </summary>
public class PickingBorderOverlayControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化 BP 选择呼吸边框覆盖控件配置。
    /// </summary>
    public PickingBorderOverlayControlConfig()
    {
        ControlType = "PickingBorderOverlay";
    }

    /// <summary>
    /// 目标 pick 控件名，仅用于描述对齐关系。
    /// </summary>
    public string TargetControlName { get; set; } = string.Empty;

    /// <summary>
    /// 边框图片路径。为空时使用 BpWindowSettings.PickingBorderImage。
    /// </summary>
    public string? BorderImagePath { get; set; }

    /// <summary>
    /// 边框填充色。为空时使用 BpWindowSettings.PickingBorderBrush。
    /// </summary>
    public string? FillColor { get; set; }

    /// <summary>
    /// 初始是否隐藏。
    /// </summary>
    public bool InitiallyHidden { get; set; } = true;
}
