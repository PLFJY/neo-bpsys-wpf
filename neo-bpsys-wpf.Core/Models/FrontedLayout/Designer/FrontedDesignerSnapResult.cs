namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器移动/缩放吸附计算的结果。
/// </summary>
public sealed class FrontedDesignerSnapResult
{
    /// <summary>
    /// 吸附后的左侧坐标。
    /// </summary>
    public double Left { get; init; }

    /// <summary>
    /// 吸附后的顶部坐标。
    /// </summary>
    public double Top { get; init; }

    /// <summary>
    /// 吸附后的宽度。
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// 吸附后的高度。
    /// </summary>
    public double Height { get; init; }

    /// <summary>
    /// 吸附对齐线列表。
    /// </summary>
    public IReadOnlyList<FrontedDesignerSnapGuide> Guides { get; init; } = [];
}

