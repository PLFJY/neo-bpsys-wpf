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
    /// 是否启用 BO3/BO5 Canvas 状态。
    /// </summary>
    public bool EnableBoModeStates { get; set; }

    /// <summary>
    /// BO 模式状态。当前仅使用 Bo3，root-level 表示默认/BO5。
    /// </summary>
    public Dictionary<string, FrontedCanvasStateConfig> BoModeStates { get; set; } = [];

    /// <summary>
    /// Canvas 使用的插件依赖元数据。
    /// </summary>
    public List<FrontedPluginDependency> RequiredPlugins { get; set; } = [];

    /// <summary>
    /// 控件配置，key 为控件名。
    /// </summary>
    public Dictionary<string, FrontedControlConfigBase> Controls { get; set; } = [];
}
