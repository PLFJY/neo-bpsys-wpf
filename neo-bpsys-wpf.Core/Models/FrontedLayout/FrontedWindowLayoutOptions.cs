namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 窗口级 Designer v3 布局选项。
/// </summary>
public class FrontedWindowLayoutOptions
{
    /// <summary>
    /// 布局选项版本。
    /// </summary>
    public int Version { get; set; } = 3;

    /// <summary>
    /// 窗口宽度（可选）。
    /// </summary>
    public double? WindowWidth { get; set; }

    /// <summary>
    /// 窗口高度（可选）。
    /// </summary>
    public double? WindowHeight { get; set; }

    /// <summary>
    /// 是否允许窗口透明。
    /// </summary>
    public bool AllowTransparency { get; set; } = true;

    /// <summary>
    /// 窗口背景颜色。
    /// </summary>
    public string? BackgroundColor { get; set; } = "#00000000";
}
