namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Describes one canvas inside a fronted window.
/// </summary>
public sealed class FrontedCanvasDescriptor
{
    /// <summary>
    /// Canvas name used by layout paths and renderer lookups, for example <c>BaseCanvas</c>.
    /// </summary>
    public string CanvasName { get; init; } = string.Empty;

    /// <summary>
    /// Fallback display name for the Designer window/canvas picker.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional localization key for the canvas display name.
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// Whether Designer v3 can load and edit this canvas layout.
    /// </summary>
    public bool Customizable { get; init; } = true;

    /// <summary>
    /// Default canvas width used when a plugin layout window creates its runtime Canvas.
    /// </summary>
    public double DefaultWidth { get; init; } = 1920D;

    /// <summary>
    /// Default canvas height used when a plugin layout window creates its runtime Canvas.
    /// </summary>
    public double DefaultHeight { get; init; } = 1080D;
}
