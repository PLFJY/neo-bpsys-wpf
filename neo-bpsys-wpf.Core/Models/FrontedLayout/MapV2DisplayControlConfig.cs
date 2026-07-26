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
    /// 地图卡片正常状态外框颜色。
    /// </summary>
    public string? MapBorderNormalColor { get; set; }

    /// <summary>
    /// 地图卡片禁用状态外框颜色。
    /// </summary>
    public string? MapBorderBannedColor { get; set; }

    /// <summary>
    /// 选图边框图片。
    /// </summary>
    public string? PickingBorderImagePath { get; set; }

    /// <summary>
    /// 选图边框填充颜色。
    /// </summary>
    public string? PickingBorderFillColor { get; set; }

    /// <summary>
    /// 可独立移动和缩放的固定内部部件。坐标相对于 MapV2Display 父控件。
    /// </summary>
    public List<MapV2InternalPartLayoutConfig> InternalParts { get; set; } = [];
}

/// <summary>
/// MapV2Display 复合控件中可独立编辑的固定内部部件。
/// </summary>
public enum MapV2InternalStylePart
{
    /// <summary>队伍名称区域。</summary>
    TeamName,
    /// <summary>地图图片和外框区域。</summary>
    MapCard,
    /// <summary>地图名称区域。</summary>
    MapName,
    /// <summary>阵营名称区域。</summary>
    CampName,
    /// <summary>选图高亮边框。</summary>
    PickingBorder
}

/// <summary>
/// MapV2Display 内部部件的相对布局配置。
/// </summary>
public sealed class MapV2InternalPartLayoutConfig
{
    /// <summary>内部部件类型。</summary>
    public MapV2InternalStylePart Part { get; set; }

    /// <summary>相对于父控件左侧的坐标。</summary>
    public double X { get; set; }

    /// <summary>相对于父控件顶部的坐标。</summary>
    public double Y { get; set; }

    /// <summary>部件宽度。</summary>
    public double Width { get; set; }

    /// <summary>部件高度。</summary>
    public double Height { get; set; }
}
