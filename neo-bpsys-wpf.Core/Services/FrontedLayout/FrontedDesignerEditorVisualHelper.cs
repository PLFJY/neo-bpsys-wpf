namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 用于设计器交互装饰的仅编辑器视觉常量。
/// </summary>
public static class FrontedDesignerEditorVisualHelper
{
    /// <summary>
    /// 普通透明命中框的基础编辑器 ZIndex。
    /// </summary>
    public const int NormalHitboxZIndexBase = 10_000;

    /// <summary>
    /// 选中控件命中框的编辑器 ZIndex。
    /// </summary>
    public const int SelectedHitboxZIndex = 20_000;

    /// <summary>
    /// 选中控件轮廓和标签的编辑器 ZIndex。
    /// </summary>
    public const int SelectedOutlineZIndex = 20_100;

    /// <summary>
    /// 选中控件调整大小手柄的编辑器 ZIndex。
    /// </summary>
    public const int SelectedHandleZIndex = 20_200;

    /// <summary>
    /// 选中轮廓的线宽。
    /// </summary>
    public const double SelectionBorderThickness = 1D;

    /// <summary>
    /// 可见手柄方形大小。
    /// </summary>
    public const double HandleVisualSize = 6D;

    /// <summary>
    /// 每个手柄周围的透明命中目标大小。
    /// </summary>
    public const double HandleHitTargetSize = 12D;

    /// <summary>
    /// 可见手柄边框线宽。
    /// </summary>
    public const double HandleBorderThickness = 1D;

    /// <summary>
    /// 画布坐标中选中标签的基础字体大小。
    /// </summary>
    public const double SelectionLabelBaseFontSize = 11D;

    /// <summary>
    /// 缩放后屏幕上选中标签的最小字体大小。
    /// </summary>
    public const double SelectionLabelMinScreenFontSize = 11D;

    /// <summary>
    /// 画布坐标中选中控件边界上方的基础垂直偏移。
    /// </summary>
    public const double SelectionLabelBaseOffset = 18D;

    /// <summary>
    /// 画布坐标中选中标签的最大字体大小。
    /// </summary>
    public const double SelectionLabelMaxCanvasFontSize = 64D;

    /// <summary>
    /// 规范化选中标签度量时使用的最小有效缩放比例。
    /// </summary>
    public const double MinValidZoomScale = 0.01D;

    /// <summary>
    /// 将无效的缩放比例规范化为安全的正值。
    /// </summary>
    public static double NormalizeZoomScale(double zoomScale)
    {
        if (double.IsNaN(zoomScale)
            || double.IsInfinity(zoomScale)
            || zoomScale < MinValidZoomScale)
        {
            return 1D;
        }

        return zoomScale;
    }

    /// <summary>
    /// 返回在给定缩放比例下保持选中标签可读性的画布空间字体大小。
    /// </summary>
    public static double GetEffectiveSelectionLabelFontSize(double zoomScale)
    {
        zoomScale = NormalizeZoomScale(zoomScale);
        var effective = Math.Max(
            SelectionLabelBaseFontSize,
            SelectionLabelMinScreenFontSize / zoomScale);
        return Math.Min(effective, SelectionLabelMaxCanvasFontSize);
    }

    /// <summary>
    /// 返回在给定缩放比例下选中控件边界上方的画布空间顶部偏移。
    /// </summary>
    public static double GetEffectiveSelectionLabelTopOffset(double zoomScale)
    {
        zoomScale = NormalizeZoomScale(zoomScale);
        return Math.Max(SelectionLabelBaseOffset, SelectionLabelBaseOffset / zoomScale);
    }

    /// <summary>
    /// 返回仅用于编辑器的命中框 ZIndex，不修改运行时布局 ZIndex。
    /// </summary>
    public static int GetHitboxZIndex(int zIndex, int layoutOrder, bool isSelected)
    {
        return isSelected
            ? SelectedHitboxZIndex
            : NormalHitboxZIndexBase + (Math.Max(0, zIndex) * 100) + layoutOrder;
    }
}
