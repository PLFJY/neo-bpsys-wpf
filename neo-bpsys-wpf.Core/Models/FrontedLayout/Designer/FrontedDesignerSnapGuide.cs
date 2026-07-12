namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器对齐线的视觉方向。
/// </summary>
public enum FrontedDesignerSnapGuideOrientation
{
    /// <summary>
    /// 垂直对齐线。
    /// </summary>
    Vertical,

    /// <summary>
    /// 水平对齐线。
    /// </summary>
    Horizontal
}

/// <summary>
/// 产生设计器对齐线的来源类型。
/// </summary>
public enum FrontedDesignerSnapGuideSource
{
    /// <summary>
    /// 画布边缘或中心。
    /// </summary>
    Canvas,

    /// <summary>
    /// 另一个可编辑控件。
    /// </summary>
    Control,

    /// <summary>
    /// 坐标网格。
    /// </summary>
    Grid
}

/// <summary>
/// 临时设计器对齐线的纯模型。
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

