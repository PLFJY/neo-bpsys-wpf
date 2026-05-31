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
    /// Returns an editor-only hitbox ZIndex without modifying runtime layout ZIndex.
    /// </summary>
    public static int GetHitboxZIndex(int zIndex, int layoutOrder, bool isSelected)
    {
        return isSelected
            ? SelectedHitboxZIndex
            : NormalHitboxZIndexBase + (Math.Max(0, zIndex) * 100) + layoutOrder;
    }
}
