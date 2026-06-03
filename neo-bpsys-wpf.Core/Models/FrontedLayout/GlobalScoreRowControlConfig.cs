using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 全局比分行控件配置。
/// </summary>
public class GlobalScoreRowControlConfig : FrontedControlConfigBase, IFrontedTextStyleConfig
{
    /// <summary>
    /// 初始化全局比分行控件配置。
    /// </summary>
    public GlobalScoreRowControlConfig()
    {
        ControlType = "GlobalScoreRow";
    }

    /// <summary>
    /// 显示主队或客队比分。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TeamType TeamType { get; set; }

    /// <summary>
    /// 设计期自动编排的主局间距。v3 运行时优先使用 <see cref="Cells"/>。
    /// </summary>
    public double MajorGameGap { get; set; } = 180;

    /// <summary>
    /// 设计期自动编排的半场间距。v3 运行时优先使用 <see cref="Cells"/>。
    /// </summary>
    public double HalfGameGap { get; set; } = 90;

    /// <summary>
    /// 行内可独立编辑的比分单元。坐标和尺寸相对于 GlobalScoreRow 父框。
    /// </summary>
    public List<GlobalScoreCellConfig> Cells { get; set; } = [];

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
    public double FontSize { get; set; } = 24;

    /// <summary>
    /// 是否显示阵营图标。
    /// </summary>
    public bool ShowCampIcon { get; set; } = true;
}

/// <summary>
/// GlobalScoreRow 内部的单个可编辑比分单元配置。
/// </summary>
public class GlobalScoreCellConfig
{
    /// <summary>
    /// 行内稳定 ID，用于设计器选择和 JSON 可读性。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 比分系统局号。
    /// </summary>
    public int GameNumber { get; set; }

    /// <summary>
    /// 普通局或加赛局。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScoreGameKind GameKind { get; set; }

    /// <summary>
    /// 上半场或下半场。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScoreHalfKind HalfKind { get; set; }

    /// <summary>
    /// 相对父行左侧坐标。
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// 相对父行顶部坐标。
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 单元宽度。
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 单元高度。
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 单元可见性。
    /// </summary>
    public FrontedControlVisibility Visibility { get; set; } = FrontedControlVisibility.Visible;

    /// <summary>
    /// 字体族；为空时继承父行。
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// 字重；为空时继承父行。
    /// </summary>
    public string? FontWeight { get; set; }

    /// <summary>
    /// 文本颜色；为空时继承父行。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 字号；为空时继承父行。
    /// </summary>
    public double? FontSize { get; set; }

    /// <summary>
    /// 是否显示阵营图标；为空时继承父行。
    /// </summary>
    public bool? ShowCampIcon { get; set; }
}
