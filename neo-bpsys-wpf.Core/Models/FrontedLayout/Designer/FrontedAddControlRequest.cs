namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Designer v3 add-control command input.
/// </summary>
public sealed class FrontedAddControlRequest
{
    /// <summary>
    /// Built-in v3 ControlType to add.
    /// </summary>
    public string ControlType { get; init; } = string.Empty;

    /// <summary>
    /// Optional logical Canvas X coordinate for the new control center.
    /// </summary>
    public double? CenterX { get; init; }

    /// <summary>
    /// Optional logical Canvas Y coordinate for the new control center.
    /// </summary>
    public double? CenterY { get; init; }
}
