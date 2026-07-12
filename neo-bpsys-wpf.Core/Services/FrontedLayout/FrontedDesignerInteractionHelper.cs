namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 点击与拖拽选择行为的纯交互规则。
/// </summary>
public static class FrontedDesignerInteractionHelper
{
    /// <summary>
    /// 仍被视为点击的最大逻辑像素移动量。
    /// </summary>
    public const double ClickThreshold = 4D;

    /// <summary>
    /// 返回指针增量是否应被视为拖拽。
    /// </summary>
    public static bool ExceedsClickThreshold(double deltaX, double deltaY)
    {
        return (deltaX * deltaX) + (deltaY * deltaY) > ClickThreshold * ClickThreshold;
    }

    /// <summary>
    /// 解析当前指针状态对应的编辑器操作。
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
/// 由设计器点击与拖拽语义选择的指针操作。
/// </summary>
public enum FrontedDesignerPointerAction
{
    /// <summary>
    /// 移动仍在点击阈值内。
    /// </summary>
    WaitForClick,

    /// <summary>
    /// 移动超过了选中控件的阈值。
    /// </summary>
    BeginDragSelected,

    /// <summary>
    /// 继续拖拽选中控件。
    /// </summary>
    DragSelected,

    /// <summary>
    /// 移动超过了未选中控件的阈值。
    /// </summary>
    IgnoreUnselectedDrag
}
