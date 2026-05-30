namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 CutScene 地图名称文本业务控件配置。
/// </summary>
public class MapNameTextControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化地图名称文本控件配置。
    /// </summary>
    public MapNameTextControlConfig()
    {
        ControlType = "MapNameText";
    }

    /// <summary>
    /// 字体族。
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// 字重。
    /// </summary>
    public string? FontWeight { get; set; }

    /// <summary>
    /// 文本颜色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 字号。
    /// </summary>
    public double FontSize { get; set; }

    /// <summary>
    /// 文本对齐。
    /// </summary>
    public string? TextAlignment { get; set; }

    /// <summary>
    /// 文本块水平对齐。
    /// </summary>
    public string? HorizontalAlignment { get; set; }

    /// <summary>
    /// 文本块垂直对齐。
    /// </summary>
    public string? VerticalAlignment { get; set; }

    /// <summary>
    /// 未选择地图时显示的文本。
    /// </summary>
    public string? EmptyText { get; set; }
}
