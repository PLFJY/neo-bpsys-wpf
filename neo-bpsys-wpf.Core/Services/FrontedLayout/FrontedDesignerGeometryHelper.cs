using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Pure geometry operations for Designer v3 in-memory editing.
/// </summary>
public static class FrontedDesignerGeometryHelper
{
    /// <summary>
    /// Default coordinate snap step.
    /// </summary>
    public const double CoordinateStep = 0.5D;

    /// <summary>
    /// Default grid size used when snap-to-grid is enabled.
    /// </summary>
    public const double DefaultSnapGridSize = 10D;

    /// <summary>
    /// Fallback hitbox width when a config has no width.
    /// </summary>
    public const double MinHitWidth = 40D;

    /// <summary>
    /// Fallback hitbox height when a config has no height.
    /// </summary>
    public const double MinHitHeight = 24D;

    /// <summary>
    /// Minimum persisted width after resize.
    /// </summary>
    public const double MinResizeWidth = 1D;

    /// <summary>
    /// Minimum persisted height after resize.
    /// </summary>
    public const double MinResizeHeight = 1D;

    /// <summary>
    /// Snaps a coordinate to the nearest half step.
    /// </summary>
    public static double Snap(double value)
    {
        return Math.Round(value / CoordinateStep, MidpointRounding.AwayFromZero) * CoordinateStep;
    }

    /// <summary>
    /// Normalizes a coordinate for free movement or grid snapping.
    /// </summary>
    public static double NormalizeCoordinate(
        double value,
        bool effectiveSnapEnabled = false,
        double snapGridSize = DefaultSnapGridSize)
    {
        if (!effectiveSnapEnabled)
        {
            return Snap(value);
        }

        var gridSize = snapGridSize > 0D ? snapGridSize : DefaultSnapGridSize;
        return Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;
    }

    /// <summary>
    /// Gets the editable hitbox width for a config.
    /// </summary>
    public static double GetEditableWidth(FrontedControlConfigBase config)
    {
        return config.Width ?? MinHitWidth;
    }

    /// <summary>
    /// Gets the editable hitbox height for a config.
    /// </summary>
    public static double GetEditableHeight(FrontedControlConfigBase config)
    {
        return config.Height ?? MinHitHeight;
    }

    /// <summary>
    /// Moves an item from an original position by a logical delta.
    /// </summary>
    public static void Move(
        FrontedControlDesignItem item,
        double originalLeft,
        double originalTop,
        double deltaX,
        double deltaY,
        FrontedCanvasDesignDocument? document = null,
        bool effectiveSnapEnabled = false,
        double snapGridSize = DefaultSnapGridSize)
    {
        item.Config.Left = NormalizeCoordinate(originalLeft + deltaX, effectiveSnapEnabled, snapGridSize);
        item.Config.Top = NormalizeCoordinate(originalTop + deltaY, effectiveSnapEnabled, snapGridSize);
        MarkDirty(document);
    }

    /// <summary>
    /// Moves an item from its current position by a logical delta.
    /// </summary>
    public static void MoveBy(
        FrontedControlDesignItem item,
        double deltaX,
        double deltaY,
        FrontedCanvasDesignDocument? document = null,
        bool effectiveSnapEnabled = false,
        double snapGridSize = DefaultSnapGridSize)
    {
        Move(
            item,
            item.Config.Left,
            item.Config.Top,
            deltaX,
            deltaY,
            document,
            effectiveSnapEnabled,
            snapGridSize);
    }

    /// <summary>
    /// Resizes an item from an original rectangle by a logical delta.
    /// </summary>
    public static void Resize(
        FrontedControlDesignItem item,
        FrontedDesignerResizeHandleKind handle,
        double originalLeft,
        double originalTop,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY,
        FrontedCanvasDesignDocument? document = null,
        bool effectiveSnapEnabled = false,
        double snapGridSize = DefaultSnapGridSize)
    {
        var left = originalLeft;
        var top = originalTop;
        var width = originalWidth;
        var height = originalHeight;

        if (AffectsLeft(handle))
        {
            left = originalLeft + deltaX;
            width = originalWidth - deltaX;
            if (width < MinResizeWidth)
            {
                left = originalLeft + originalWidth - MinResizeWidth;
                width = MinResizeWidth;
            }
        }

        if (AffectsRight(handle))
        {
            width = Math.Max(MinResizeWidth, originalWidth + deltaX);
        }

        if (AffectsTop(handle))
        {
            top = originalTop + deltaY;
            height = originalHeight - deltaY;
            if (height < MinResizeHeight)
            {
                top = originalTop + originalHeight - MinResizeHeight;
                height = MinResizeHeight;
            }
        }

        if (AffectsBottom(handle))
        {
            height = Math.Max(MinResizeHeight, originalHeight + deltaY);
        }

        item.Config.Left = NormalizeCoordinate(left, effectiveSnapEnabled, snapGridSize);
        item.Config.Top = NormalizeCoordinate(top, effectiveSnapEnabled, snapGridSize);
        item.Config.Width = Math.Max(
            MinResizeWidth,
            NormalizeCoordinate(width, effectiveSnapEnabled, snapGridSize));
        item.Config.Height = Math.Max(
            MinResizeHeight,
            NormalizeCoordinate(height, effectiveSnapEnabled, snapGridSize));
        MarkDirty(document);
    }

    /// <summary>
    /// Resizes an item from its current editable rectangle by a logical delta.
    /// </summary>
    public static void ResizeBy(
        FrontedControlDesignItem item,
        FrontedDesignerResizeHandleKind handle,
        double deltaX,
        double deltaY,
        FrontedCanvasDesignDocument? document = null,
        bool effectiveSnapEnabled = false,
        double snapGridSize = DefaultSnapGridSize)
    {
        Resize(
            item,
            handle,
            item.Config.Left,
            item.Config.Top,
            GetEditableWidth(item.Config),
            GetEditableHeight(item.Config),
            deltaX,
            deltaY,
            document,
            effectiveSnapEnabled,
            snapGridSize);
    }

    private static bool AffectsLeft(FrontedDesignerResizeHandleKind handle)
    {
        return handle is FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.Left
            or FrontedDesignerResizeHandleKind.BottomLeft;
    }

    private static bool AffectsRight(FrontedDesignerResizeHandleKind handle)
    {
        return handle is FrontedDesignerResizeHandleKind.TopRight
            or FrontedDesignerResizeHandleKind.Right
            or FrontedDesignerResizeHandleKind.BottomRight;
    }

    private static bool AffectsTop(FrontedDesignerResizeHandleKind handle)
    {
        return handle is FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.Top
            or FrontedDesignerResizeHandleKind.TopRight;
    }

    private static bool AffectsBottom(FrontedDesignerResizeHandleKind handle)
    {
        return handle is FrontedDesignerResizeHandleKind.BottomLeft
            or FrontedDesignerResizeHandleKind.Bottom
            or FrontedDesignerResizeHandleKind.BottomRight;
    }

    private static void MarkDirty(FrontedCanvasDesignDocument? document)
    {
        if (document is not null)
        {
            document.IsDirty = true;
        }
    }
}
