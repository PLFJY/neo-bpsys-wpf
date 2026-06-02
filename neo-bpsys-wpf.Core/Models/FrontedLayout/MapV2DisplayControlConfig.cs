namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 地图 BP v2 展示控件配置。
/// </summary>
public class MapV2DisplayControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化地图 BP v2 展示控件配置。
    /// </summary>
    public MapV2DisplayControlConfig()
    {
        ControlType = "MapV2Display";
    }

    /// <summary>
    /// 地图字典 key。
    /// </summary>
    public string MapKey { get; set; } = string.Empty;

    /// <summary>
    /// 地图名称字体族。
    /// </summary>
    public string? MapNameFontFamily { get; set; }

    /// <summary>
    /// 地图名称字重。
    /// </summary>
    public string? MapNameFontWeight { get; set; }

    /// <summary>
    /// 地图名称颜色。
    /// </summary>
    public string? MapNameColor { get; set; }

    /// <summary>
    /// 地图名称字号。
    /// </summary>
    public double MapNameFontSize { get; set; }

    /// <summary>
    /// 队名字体族。
    /// </summary>
    public string? TeamNameFontFamily { get; set; }

    /// <summary>
    /// 队名字重。
    /// </summary>
    public string? TeamNameFontWeight { get; set; }

    /// <summary>
    /// 队名颜色。
    /// </summary>
    public string? TeamNameColor { get; set; }

    /// <summary>
    /// 队名字号。
    /// </summary>
    public double TeamNameFontSize { get; set; }

    /// <summary>
    /// 阵营文字字体族。
    /// </summary>
    public string? CampNameFontFamily { get; set; }

    /// <summary>
    /// 阵营文字字重。
    /// </summary>
    public string? CampNameFontWeight { get; set; }

    /// <summary>
    /// 阵营文字颜色。
    /// </summary>
    public string? CampNameColor { get; set; }

    /// <summary>
    /// 阵营文字字号。
    /// </summary>
    public double CampNameFontSize { get; set; }

    /// <summary>
    /// 选图边框图片。
    /// </summary>
    public string? PickingBorderImagePath { get; set; }

    /// <summary>
    /// 选图边框填充颜色。
    /// </summary>
    public string? PickingBorderFillColor { get; set; }
}
