namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Result of a designer move/resize snap calculation.
/// </summary>
public sealed class FrontedDesignerSnapResult
{
    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public IReadOnlyList<FrontedDesignerSnapGuide> Guides { get; init; } = [];
}

