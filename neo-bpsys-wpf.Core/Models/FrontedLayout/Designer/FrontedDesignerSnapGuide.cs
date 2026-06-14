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
    /// <summary>
    /// 对齐方向。
    /// </summary>
    public FrontedDesignerSnapGuideOrientation Orientation { get; init; }

    /// <summary>
    /// 对齐位置。
    /// </summary>
    public double Position { get; init; }

    /// <summary>
    /// 线段的起始位置。
    /// </summary>
    public double Start { get; init; }

    /// <summary>
    /// 线段的结束位置。
    /// </summary>
    public double End { get; init; }

    /// <summary>
    /// 对齐线的来源。
    /// </summary>
    public FrontedDesignerSnapGuideSource Source { get; init; }

    /// <summary>
    /// 对齐线标签。
    /// </summary>
    public string? Label { get; init; }
}

