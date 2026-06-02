namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Visual orientation for a designer snap guide.
/// </summary>
public enum FrontedDesignerSnapGuideOrientation
{
    /// <summary>
    /// Vertical alignment guide.
    /// </summary>
    Vertical,

    /// <summary>
    /// Horizontal alignment guide.
    /// </summary>
    Horizontal
}

/// <summary>
/// Source type that produced a designer snap guide.
/// </summary>
public enum FrontedDesignerSnapGuideSource
{
    /// <summary>
    /// Canvas edge or center.
    /// </summary>
    Canvas,

    /// <summary>
    /// Another editable control.
    /// </summary>
    Control,

    /// <summary>
    /// Coordinate grid.
    /// </summary>
    Grid
}

/// <summary>
/// Pure model for a transient designer snap guide line.
/// </summary>
public sealed class FrontedDesignerSnapGuide
{
    public FrontedDesignerSnapGuideOrientation Orientation { get; init; }

    public double Position { get; init; }

    public double Start { get; init; }

    public double End { get; init; }

    public FrontedDesignerSnapGuideSource Source { get; init; }

    public string? Label { get; init; }
}

