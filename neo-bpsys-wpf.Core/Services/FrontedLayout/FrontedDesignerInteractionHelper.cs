namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Pure interaction rules for click-vs-drag selection behavior.
/// </summary>
public static class FrontedDesignerInteractionHelper
{
    /// <summary>
    /// Maximum logical-pixel movement that is still treated as a click.
    /// </summary>
    public const double ClickThreshold = 4D;

    /// <summary>
    /// Returns whether a pointer delta should be treated as a drag.
    /// </summary>
    public static bool ExceedsClickThreshold(double deltaX, double deltaY)
    {
        return (deltaX * deltaX) + (deltaY * deltaY) > ClickThreshold * ClickThreshold;
    }

    /// <summary>
    /// Resolves the editor action for the current pointer state.
    /// </summary>
    public static FrontedDesignerPointerAction ResolvePointerAction(
        bool thresholdExceeded,
        bool candidateIsSelected,
        bool isDraggingSelected)
    {
        if (!thresholdExceeded)
        {
            return FrontedDesignerPointerAction.WaitForClick;
        }

        if (isDraggingSelected)
        {
            return FrontedDesignerPointerAction.DragSelected;
        }

        return candidateIsSelected
            ? FrontedDesignerPointerAction.BeginDragSelected
            : FrontedDesignerPointerAction.IgnoreUnselectedDrag;
    }
}

/// <summary>
/// Pointer action chosen by designer click-vs-drag semantics.
/// </summary>
public enum FrontedDesignerPointerAction
{
    /// <summary>
    /// Movement is still within click threshold.
    /// </summary>
    WaitForClick,

    /// <summary>
    /// Movement crossed the threshold on the selected control.
    /// </summary>
    BeginDragSelected,

    /// <summary>
    /// Continue dragging the selected control.
    /// </summary>
    DragSelected,

    /// <summary>
    /// Movement crossed the threshold on an unselected control.
    /// </summary>
    IgnoreUnselectedDrag
}
