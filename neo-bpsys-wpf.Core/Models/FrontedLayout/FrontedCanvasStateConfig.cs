namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 设计器 v3 运行时状态选择所使用的可选画布状态。
/// </summary>
public class FrontedCanvasStateConfig
{
    /// <summary>
    /// 状态专属背景图片路径。
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// 状态专属插件依赖。
    /// </summary>
    public List<FrontedPluginDependency> RequiredPlugins { get; set; } = [];

    /// <summary>
    /// 状态专属控件，以控件名为键。
    /// </summary>
    public Dictionary<string, FrontedControlConfigBase> Controls { get; set; } = [];
}
