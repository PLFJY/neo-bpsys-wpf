using System.Text.Json.Serialization;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Json;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 前台 Canvas 配置。
/// </summary>
[JsonConverter(typeof(FrontedCanvasConfigJsonConverter))]
public class FrontedCanvasConfig
{
    /// <summary>
    /// 布局版本。
    /// </summary>
    public int Version { get; set; } = 3;

    /// <summary>
    /// Canvas 宽度。
    /// </summary>
    public double CanvasWidth { get; set; }

    /// <summary>
    /// Canvas 高度。
    /// </summary>
    public double CanvasHeight { get; set; }

    /// <summary>
    /// 背景图片路径。
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// 控件配置，key 为控件名。
    /// </summary>
    public Dictionary<string, FrontedControlConfigBase> Controls { get; set; } = [];
}
