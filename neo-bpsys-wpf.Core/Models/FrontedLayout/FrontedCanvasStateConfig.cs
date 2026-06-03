namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Optional Canvas state used by Designer v3 runtime state selection.
/// </summary>
public class FrontedCanvasStateConfig
{
    /// <summary>
    /// State-specific background image path.
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// State-specific plugin dependencies.
    /// </summary>
    public List<FrontedPluginDependency> RequiredPlugins { get; set; } = [];

    /// <summary>
    /// State-specific controls keyed by control name.
    /// </summary>
    public Dictionary<string, FrontedControlConfigBase> Controls { get; set; } = [];
}
