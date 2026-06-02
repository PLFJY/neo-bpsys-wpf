namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Describes one canvas inside a fronted window.
/// </summary>
public sealed class FrontedCanvasDescriptor
{
    public string CanvasName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? DisplayNameKey { get; init; }

    public bool Customizable { get; init; } = true;

    public double DefaultWidth { get; init; } = 1920D;

    public double DefaultHeight { get; init; } = 1080D;
}
