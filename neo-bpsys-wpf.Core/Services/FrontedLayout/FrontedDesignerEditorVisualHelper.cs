namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Editor-only visual constants for designer interaction chrome.
/// </summary>
public static class FrontedDesignerEditorVisualHelper
{
    /// <summary>
    /// Base editor ZIndex for normal transparent hitboxes.
    /// </summary>
    public const int NormalHitboxZIndexBase = 10_000;

    /// <summary>
    /// Editor ZIndex for the selected control hitbox.
    /// </summary>
    public const int SelectedHitboxZIndex = 20_000;

    /// <summary>
    /// Editor ZIndex for the selected control outline and label.
    /// </summary>
    public const int SelectedOutlineZIndex = 20_100;

    /// <summary>
    /// Editor ZIndex for selected control resize handles.
    /// </summary>
    public const int SelectedHandleZIndex = 20_200;

    /// <summary>
    /// Selection outline thickness.
    /// </summary>
    public const double SelectionBorderThickness = 1D;

    /// <summary>
    /// Visible handle square size.
    /// </summary>
    public const double HandleVisualSize = 6D;

    /// <summary>
    /// Transparent hit target size around each handle.
    /// </summary>
    public const double HandleHitTargetSize = 12D;

    /// <summary>
    /// Visible handle border thickness.
    /// </summary>
    public const double HandleBorderThickness = 1D;

    /// <summary>
    /// Base selection label font size in canvas coordinates.
    /// </summary>
    public const double SelectionLabelBaseFontSize = 11D;

    /// <summary>
    /// Minimum on-screen selection label font size after zoom scaling.
    /// </summary>
    public const double SelectionLabelMinScreenFontSize = 11D;

    /// <summary>
    /// Base vertical offset above the selected control bounds in canvas coordinates.
    /// </summary>
    public const double SelectionLabelBaseOffset = 18D;

    /// <summary>
    /// Maximum selection label font size in canvas coordinates.
    /// </summary>
    public const double SelectionLabelMaxCanvasFontSize = 64D;

    /// <summary>
    /// Minimum valid zoom scale used when normalizing selection label metrics.
    /// </summary>
    public const double MinValidZoomScale = 0.01D;

    /// <summary>
    /// Normalizes an invalid zoom scale to a safe positive value.
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
    /// Returns the canvas-space font size that keeps the selection label readable at the given zoom.
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
    /// Returns the canvas-space top offset above the selected control bounds at the given zoom.
    /// </summary>
    public static double GetEffectiveSelectionLabelTopOffset(double zoomScale)
    {
        zoomScale = NormalizeZoomScale(zoomScale);
        return Math.Max(SelectionLabelBaseOffset, SelectionLabelBaseOffset / zoomScale);
    }

    /// <summary>
    /// Returns an editor-only hitbox ZIndex without modifying runtime layout ZIndex.
    /// </summary>
    public static int GetHitboxZIndex(int zIndex, int layoutOrder, bool isSelected)
    {
        return isSelected
            ? SelectedHitboxZIndex
            : NormalHitboxZIndexBase + (Math.Max(0, zIndex) * 100) + layoutOrder;
    }
}
