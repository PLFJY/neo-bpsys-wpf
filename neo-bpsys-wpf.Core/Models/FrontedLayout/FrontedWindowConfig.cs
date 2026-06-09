using System.Text.Json.Serialization;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Json;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Window-centric Designer v3 fronted layout configuration.
/// </summary>
public sealed class FrontedWindowConfig
{
    /// <summary>
    /// Layout schema version.
    /// </summary>
    public int Version { get; set; } = 3;

    /// <summary>
    /// Settings applied to the WPF output window.
    /// </summary>
    public FrontedWindowSettings WindowSettings { get; set; } = new();

    /// <summary>
    /// Settings applied to the internal <c>BaseCanvas</c>.
    /// </summary>
    public FrontedCanvasSettings CanvasSettings { get; set; } = new();

    /// <summary>
    /// Control dependencies and control configurations rendered by the fronted renderer.
    /// </summary>
    public FrontedControlLayout ControlLayout { get; set; } = new();

    /// <summary>
    /// Creates a window-centric config from the legacy canvas-centric v3 model.
    /// </summary>
    /// <param name="canvasConfig">The canvas-centric config to convert.</param>
    /// <returns>A new window-centric config.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="canvasConfig"/> is null.</exception>
    public static FrontedWindowConfig FromCanvasConfig(FrontedCanvasConfig canvasConfig)
    {
        ArgumentNullException.ThrowIfNull(canvasConfig);

        return new FrontedWindowConfig
        {
            Version = 3,
            WindowSettings = new FrontedWindowSettings
            {
                WindowWidth = canvasConfig.CanvasWidth,
                WindowHeight = canvasConfig.CanvasHeight
            },
            CanvasSettings = new FrontedCanvasSettings
            {
                CanvasWidth = canvasConfig.CanvasWidth,
                CanvasHeight = canvasConfig.CanvasHeight,
                BackgroundImage = canvasConfig.BackgroundImage,
                EnableBoModeStates = canvasConfig.EnableBoModeStates,
                BoModeStates = canvasConfig.BoModeStates
            },
            ControlLayout = new FrontedControlLayout
            {
                RequiredPlugins = canvasConfig.RequiredPlugins,
                Controls = canvasConfig.Controls
            }
        };
    }

    /// <summary>
    /// Converts this window-centric config to the legacy canvas-centric model for temporary helper paths.
    /// </summary>
    /// <returns>A canvas-centric config containing the same canvas and control data.</returns>
    public FrontedCanvasConfig ToCanvasConfig()
    {
        return new FrontedCanvasConfig
        {
            Version = Version,
            CanvasWidth = CanvasSettings.CanvasWidth,
            CanvasHeight = CanvasSettings.CanvasHeight,
            BackgroundImage = CanvasSettings.BackgroundImage,
            EnableBoModeStates = CanvasSettings.EnableBoModeStates,
            BoModeStates = CanvasSettings.BoModeStates,
            RequiredPlugins = ControlLayout.RequiredPlugins,
            Controls = ControlLayout.Controls
        };
    }

    /// <summary>
    /// Copies the internal canvas size to the WPF window size for explicit legacy conversion paths.
    /// </summary>
    /// <remarks>
    /// Normal v3 layout reads, saves, imports, and exports must preserve <see cref="WindowSettings"/>. Use this
    /// helper only when converting an older canvas-centric layout that has no independent window size.
    /// </remarks>
    public void SyncWindowSizeToCanvas()
    {
        if (IsPositiveFinite(CanvasSettings.CanvasWidth))
        {
            WindowSettings.WindowWidth = CanvasSettings.CanvasWidth;
        }

        if (IsPositiveFinite(CanvasSettings.CanvasHeight))
        {
            WindowSettings.WindowHeight = CanvasSettings.CanvasHeight;
        }
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0D;
    }
}

/// <summary>
/// Window-level settings for a window-centric Designer v3 layout.
/// </summary>
public sealed class FrontedWindowSettings
{
    /// <summary>
    /// WPF window width.
    /// </summary>
    public double WindowWidth { get; set; } = 1440D;

    /// <summary>
    /// WPF window height.
    /// </summary>
    public double WindowHeight { get; set; } = 810D;

    /// <summary>
    /// Optional WPF window left coordinate.
    /// </summary>
    public double? WindowLeft { get; set; }

    /// <summary>
    /// Optional WPF window top coordinate.
    /// </summary>
    public double? WindowTop { get; set; }

    /// <summary>
    /// Whether the WPF window allows transparency.
    /// </summary>
    public bool AllowsTransparency { get; set; } = true;

    /// <summary>
    /// Window background color in <c>#AARRGGBB</c> format.
    /// </summary>
    public string? BackgroundColor { get; set; } = "#00000000";

    /// <summary>
    /// Whether the WPF window is topmost.
    /// </summary>
    public bool Topmost { get; set; }

    /// <summary>
    /// Stretch mode used by the internal ViewBox. Serialized as a string enum name.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Stretch ViewboxStretch { get; set; } = Stretch.Fill;
}

/// <summary>
/// Settings applied to the internal <c>BaseCanvas</c> of a window-centric layout.
/// </summary>
[JsonConverter(typeof(FrontedCanvasSettingsJsonConverter))]
public sealed class FrontedCanvasSettings
{
    /// <summary>
    /// Internal canvas width.
    /// </summary>
    public double CanvasWidth { get; set; } = 1440D;

    /// <summary>
    /// Internal canvas height.
    /// </summary>
    public double CanvasHeight { get; set; } = 810D;

    /// <summary>
    /// Internal canvas background image path.
    /// </summary>
    public string? BackgroundImage { get; set; }

    /// <summary>
    /// Whether BO mode canvas states are enabled.
    /// </summary>
    public bool EnableBoModeStates { get; set; }

    /// <summary>
    /// BO mode states. Current runtime uses <c>Bo3</c>; root values represent default/BO5.
    /// </summary>
    public Dictionary<string, FrontedCanvasStateConfig> BoModeStates { get; set; } = [];
}

/// <summary>
/// Control dependencies and control configs rendered inside a window-centric layout.
/// </summary>
[JsonConverter(typeof(FrontedControlLayoutJsonConverter))]
public sealed class FrontedControlLayout
{
    /// <summary>
    /// Plugin dependencies required by this control layout.
    /// </summary>
    public List<FrontedPluginDependency> RequiredPlugins { get; set; } = [];

    /// <summary>
    /// Control configs keyed by control name.
    /// </summary>
    public Dictionary<string, FrontedControlConfigBase> Controls { get; set; } = [];
}
