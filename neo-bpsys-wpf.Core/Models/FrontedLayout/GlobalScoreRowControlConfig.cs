using neo_bpsys_wpf.Core.Enums;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 全局比分行控件配置。
/// </summary>
public class GlobalScoreRowControlConfig : FrontedControlConfigBase
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
    /// 每个比分系统 Game 之间的水平间距。
    /// </summary>
    public double MajorGameGap { get; set; } = 180;

    /// <summary>
    /// 同一 Game 内上下半场之间的水平间距。
    /// </summary>
    public double HalfGameGap { get; set; } = 90;

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
