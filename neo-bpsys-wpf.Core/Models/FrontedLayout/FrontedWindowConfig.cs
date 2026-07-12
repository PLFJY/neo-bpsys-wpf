using System.Text.Json.Serialization;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Json;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 以窗口为中心的设计器 v3 前台布局配置。
/// </summary>
public sealed class FrontedWindowConfig
{
    /// <summary>
    /// 布局架构版本。
    /// </summary>
    public int Version { get; set; } = 3;

    /// <summary>
    /// 应用于 WPF 输出窗口的设置。
    /// </summary>
    public FrontedWindowSettings WindowSettings { get; set; } = new();

    /// <summary>
    /// 应用于内部 <c>BaseCanvas</c> 的设置。
    /// </summary>
    public FrontedCanvasSettings CanvasSettings { get; set; } = new();

    /// <summary>
    /// 由前台渲染器渲染的控件依赖和控件配置。
    /// </summary>
    public FrontedControlLayout ControlLayout { get; set; } = new();

}

/// <summary>
/// 以窗口为中心的设计器 v3 布局的窗口级设置。
/// </summary>
public sealed class FrontedWindowSettings
{
    /// <summary>
    /// WPF 窗口宽度。
    /// </summary>
    public double WindowWidth { get; set; } = 1440D;

    /// <summary>
    /// WPF 窗口高度。
    /// </summary>
    public double WindowHeight { get; set; } = 810D;

    /// <summary>
    /// 可选的 WPF 窗口左侧坐标。
    /// </summary>
    public double? WindowLeft { get; set; }

    /// <summary>
    /// 可选的 WPF 窗口顶部坐标。
    /// </summary>
    public double? WindowTop { get; set; }

    /// <summary>
    /// WPF 窗口是否允许透明。
    /// </summary>
    public bool AllowsTransparency { get; set; } = true;

    /// <summary>
    /// 窗口背景色，格式为 <c>#AARRGGBB</c>。
    /// </summary>
    public string? BackgroundColor { get; set; } = "#00000000";

    /// <summary>
    /// WPF 窗口是否置顶。
    /// </summary>
    public bool Topmost { get; set; }

    /// <summary>
    /// 内部 ViewBox 使用的拉伸模式。序列化为字符串枚举名。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Stretch ViewboxStretch { get; set; } = Stretch.Fill;
}

/// <summary>
/// 应用于以窗口为中心的布局内部 <c>BaseCanvas</c> 的设置。
/// </summary>
[JsonConverter(typeof(FrontedCanvasSettingsJsonConverter))]
public sealed class FrontedCanvasSettings
{
    /// <summary>
    /// 内部画布宽度。
    /// </summary>
    public double CanvasWidth { get; set; } = 1440D;

    /// <summary>
    /// 内部画布高度。
    /// </summary>
    public double CanvasHeight { get; set; } = 810D;

    /// <summary>
    /// 内部画布背景图片路径。
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// 是否启用 BO 模式画布状态。
    /// </summary>
    public bool EnableBoModeStates { get; set; }

    /// <summary>
    /// BO 模式状态。当前运行时使用 <c>Bo3</c>；根值表示默认/BO5。
    /// </summary>
    public Dictionary<string, FrontedCanvasStateConfig> BoModeStates { get; set; } = [];
}

/// <summary>
/// 以窗口为中心的布局内部渲染的控件依赖和控件配置。
/// </summary>
[JsonConverter(typeof(FrontedControlLayoutJsonConverter))]
public sealed class FrontedControlLayout
{
    /// <summary>
    /// 此控件布局所需的插件依赖。
    /// </summary>
    public List<FrontedPluginDependency> RequiredPlugins { get; set; } = [];

    /// <summary>
    /// 控件配置，以控件名为键。
    /// </summary>
    public Dictionary<string, FrontedControlConfigBase> Controls { get; set; } = [];
}
