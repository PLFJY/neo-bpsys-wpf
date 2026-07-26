using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 设计器 v3 移动/调整大小交互的纯智能对齐吸附。
/// </summary>
public static class FrontedDesignerSmartSnapHelper
{
    public const double DefaultScreenTolerance = 6D;

    public const double MinLogicalTolerance = 2D;

    public const double MaxLogicalTolerance = 12D;

    public static double CalculateLogicalTolerance(double zoomScale)
    {
        if (!double.IsFinite(zoomScale) || zoomScale <= 0D)
        {
            zoomScale = 1D;
        }

        return Math.Clamp(DefaultScreenTolerance / zoomScale, MinLogicalTolerance, MaxLogicalTolerance);
    }

    public static FrontedDesignerSnapResult Move(
        FrontedControlDesignItem item,
        FrontedCanvasDesignDocument? document,
        double originalLeft,
        double originalTop,
        double width,
        double height,
        double deltaX,
        double deltaY,
        bool effectiveSnapEnabled,
        double snapGridSize,
        double logicalTolerance)
    {
        var proposedLeft = originalLeft + deltaX;
        var proposedTop = originalTop + deltaY;

        if (!effectiveSnapEnabled)
        {
            return new FrontedDesignerSnapResult
            {
                Left = FrontedDesignerGeometryHelper.Snap(proposedLeft),
                Top = FrontedDesignerGeometryHelper.Snap(proposedTop),
                Width = width,
                Height = height
            };
        }

        var guides = new List<FrontedDesignerSnapGuide>(2);
        var candidates = BuildCandidates(document, item);

        if (TryFindBestXAxisSnap(proposedLeft, width, candidates, logicalTolerance, out var xSnap))
        {
            proposedLeft += xSnap.Offset;
            guides.Add(CreateGuide(
                FrontedDesignerSnapGuideOrientation.Vertical,
                xSnap.Candidate.Position,
                document?.CanvasConfig.CanvasHeight ?? 0D,
                xSnap.Candidate.Source,
                xSnap.Candidate.Label));
        }
        else
        {
            proposedLeft = FrontedDesignerGeometryHelper.NormalizeCoordinate(
                proposedLeft,
                effectiveSnapEnabled: true,
                snapGridSize);
        }

        if (TryFindBestYAxisSnap(proposedTop, height, candidates, logicalTolerance, out var ySnap))
        {
            proposedTop += ySnap.Offset;
            guides.Add(CreateGuide(
                FrontedDesignerSnapGuideOrientation.Horizontal,
                ySnap.Candidate.Position,
                document?.CanvasConfig.CanvasWidth ?? 0D,
                ySnap.Candidate.Source,
                ySnap.Candidate.Label));
        }
        else
        {
            proposedTop = FrontedDesignerGeometryHelper.NormalizeCoordinate(
                proposedTop,
                effectiveSnapEnabled: true,
                snapGridSize);
        }

        return new FrontedDesignerSnapResult
        {
            Left = proposedLeft,
            Top = proposedTop,
            Width = width,
            Height = height,
            Guides = guides
        };
    }

    public static FrontedDesignerSnapResult Resize(
        FrontedControlDesignItem item,
        FrontedCanvasDesignDocument? document,
        FrontedDesignerResizeHandleKind handle,
        double originalLeft,
        double originalTop,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY,
        bool effectiveSnapEnabled,
        double snapGridSize,
        double logicalTolerance)
    {
        var rect = CalculateResizeRect(
            handle,
            originalLeft,
            originalTop,
            originalWidth,
            originalHeight,
            deltaX,
            deltaY);

        if (!effectiveSnapEnabled)
        {
            return new FrontedDesignerSnapResult
            {
                Left = FrontedDesignerGeometryHelper.Snap(rect.Left),
                Top = FrontedDesignerGeometryHelper.Snap(rect.Top),
                Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, FrontedDesignerGeometryHelper.Snap(rect.Width)),
                Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, FrontedDesignerGeometryHelper.Snap(rect.Height))
            };
        }

        var guides = new List<FrontedDesignerSnapGuide>(2);
        var candidates = BuildCandidates(document, item);

        if (TryFindResizeXAxisSnap(rect, handle, candidates, logicalTolerance, out var xSnap))
        {
            ApplyResizeXAxisSnap(ref rect, handle, xSnap.Candidate.Position);
            guides.Add(CreateGuide(
                FrontedDesignerSnapGuideOrientation.Vertical,
                xSnap.Candidate.Position,
                document?.CanvasConfig.CanvasHeight ?? 0D,
                xSnap.Candidate.Source,
                xSnap.Candidate.Label));
        }
        else
        {
            NormalizeResizeXAxisFallback(ref rect, handle, snapGridSize);
        }

        if (TryFindResizeYAxisSnap(rect, handle, candidates, logicalTolerance, out var ySnap))
        {
            ApplyResizeYAxisSnap(ref rect, handle, ySnap.Candidate.Position);
            guides.Add(CreateGuide(
                FrontedDesignerSnapGuideOrientation.Horizontal,
                ySnap.Candidate.Position,
                document?.CanvasConfig.CanvasWidth ?? 0D,
                ySnap.Candidate.Source,
                ySnap.Candidate.Label));
        }
        else
        {
            NormalizeResizeYAxisFallback(ref rect, handle, snapGridSize);
        }

        return new FrontedDesignerSnapResult
        {
            Left = rect.Left,
            Top = rect.Top,
            Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, rect.Width),
            Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, rect.Height),
            Guides = guides
        };
    }

    private static ResizeRect CalculateResizeRect(
        FrontedDesignerResizeHandleKind handle,
        double originalLeft,
        double originalTop,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY)
    {
        var left = originalLeft;
        var top = originalTop;
        var width = originalWidth;
        var height = originalHeight;

        if (AffectsLeft(handle))
        {
            left = originalLeft + deltaX;
            width = originalWidth - deltaX;
            if (width < FrontedDesignerGeometryHelper.MinResizeWidth)
            {
                left = originalLeft + originalWidth - FrontedDesignerGeometryHelper.MinResizeWidth;
                width = FrontedDesignerGeometryHelper.MinResizeWidth;
            }
        }

        if (AffectsRight(handle))
        {
            width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, originalWidth + deltaX);
        }

        if (AffectsTop(handle))
        {
            top = originalTop + deltaY;
            height = originalHeight - deltaY;
            if (height < FrontedDesignerGeometryHelper.MinResizeHeight)
            {
                top = originalTop + originalHeight - FrontedDesignerGeometryHelper.MinResizeHeight;
                height = FrontedDesignerGeometryHelper.MinResizeHeight;
            }
        }

        if (AffectsBottom(handle))
        {
            height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, originalHeight + deltaY);
        }

        return new ResizeRect(left, top, width, height);
    }

    private static SnapCandidateSet BuildCandidates(
        FrontedCanvasDesignDocument? document,
        FrontedControlDesignItem activeItem)
    {
        var x = new List<SnapCandidate>();
        var y = new List<SnapCandidate>();

        var canvas = document?.CanvasConfig;
        if (canvas is not null)
        {
            AddCandidate(x, 0D, FrontedDesignerSnapGuideSource.Canvas, "CanvasLeft");
            AddCandidate(x, canvas.CanvasWidth / 2D, FrontedDesignerSnapGuideSource.Canvas, "CanvasCenterX");
            AddCandidate(x, canvas.CanvasWidth, FrontedDesignerSnapGuideSource.Canvas, "CanvasRight");
            AddCandidate(y, 0D, FrontedDesignerSnapGuideSource.Canvas, "CanvasTop");
            AddCandidate(y, canvas.CanvasHeight / 2D, FrontedDesignerSnapGuideSource.Canvas, "CanvasCenterY");
            AddCandidate(y, canvas.CanvasHeight, FrontedDesignerSnapGuideSource.Canvas, "CanvasBottom");
        }

        if (document is null)
        {
            return new SnapCandidateSet(x, y);
        }

        foreach (var item in document.Controls)
        {
            if (ReferenceEquals(item, activeItem)
                || !item.IsSelectableInEditor
                || !item.IsEditableInEditor
                || item.IsLinkedOverlay)
            {
                continue;
            }

            var bounds = FrontedDesignerBoundsResolver.Resolve(item.Config);
            if (!IsValidBounds(bounds))
            {
                continue;
            }

            AddCandidate(x, bounds.Left, FrontedDesignerSnapGuideSource.Control, item.Name);
            AddCandidate(x, bounds.Left + bounds.Width / 2D, FrontedDesignerSnapGuideSource.Control, item.Name);
            AddCandidate(x, bounds.Left + bounds.Width, FrontedDesignerSnapGuideSource.Control, item.Name);
            AddCandidate(y, bounds.Top, FrontedDesignerSnapGuideSource.Control, item.Name);
            AddCandidate(y, bounds.Top + bounds.Height / 2D, FrontedDesignerSnapGuideSource.Control, item.Name);
            AddCandidate(y, bounds.Top + bounds.Height, FrontedDesignerSnapGuideSource.Control, item.Name);
        }

        return new SnapCandidateSet(x, y);
    }

    private static bool TryFindBestXAxisSnap(
        double left,
        double width,
        SnapCandidateSet candidates,
        double tolerance,
        out SnapMatch match)
    {
        return TryFindBestSnap(
            [left, left + width / 2D, left + width],
            candidates.X,
            tolerance,
            out match);
    }

    private static bool TryFindBestYAxisSnap(
        double top,
        double height,
        SnapCandidateSet candidates,
        double tolerance,
        out SnapMatch match)
    {
        return TryFindBestSnap(
            [top, top + height / 2D, top + height],
            candidates.Y,
            tolerance,
            out match);
    }

    private static bool TryFindResizeXAxisSnap(
        ResizeRect rect,
        FrontedDesignerResizeHandleKind handle,
        SnapCandidateSet candidates,
        double tolerance,
        out SnapMatch match)
    {
        if (AffectsLeft(handle))
        {
            return TryFindBestSnap([rect.Left], candidates.X, tolerance, out match);
        }

        if (AffectsRight(handle))
        {
            return TryFindBestSnap([rect.Left + rect.Width], candidates.X, tolerance, out match);
        }

        match = default;
        return false;
    }

    private static bool TryFindResizeYAxisSnap(
        ResizeRect rect,
        FrontedDesignerResizeHandleKind handle,
        SnapCandidateSet candidates,
        double tolerance,
        out SnapMatch match)
    {
        if (AffectsTop(handle))
        {
            return TryFindBestSnap([rect.Top], candidates.Y, tolerance, out match);
        }

        if (AffectsBottom(handle))
        {
            return TryFindBestSnap([rect.Top + rect.Height], candidates.Y, tolerance, out match);
        }

        match = default;
        return false;
    }

    private static bool TryFindBestSnap(
        IReadOnlyList<double> activePositions,
        IReadOnlyList<SnapCandidate> candidates,
        double tolerance,
        out SnapMatch match)
    {
        match = default;
        var bestDistance = double.MaxValue;

        foreach (var activePosition in activePositions)
        {
            if (!double.IsFinite(activePosition))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                var distance = candidate.Position - activePosition;
                var absoluteDistance = Math.Abs(distance);
                if (absoluteDistance <= tolerance && absoluteDistance < bestDistance)
                {
                    bestDistance = absoluteDistance;
                    match = new SnapMatch(candidate, distance);
                }
            }
        }

        return bestDistance < double.MaxValue;
    }

    private static void ApplyResizeXAxisSnap(
        ref ResizeRect rect,
        FrontedDesignerResizeHandleKind handle,
        double position)
    {
        if (AffectsLeft(handle))
        {
            var right = rect.Left + rect.Width;
            rect.Left = Math.Min(position, right - FrontedDesignerGeometryHelper.MinResizeWidth);
            rect.Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, right - rect.Left);
        }
        else if (AffectsRight(handle))
        {
            rect.Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, position - rect.Left);
        }
    }

    private static void NormalizeResizeXAxisFallback(
        ref ResizeRect rect,
        FrontedDesignerResizeHandleKind handle,
        double snapGridSize)
    {
        if (AffectsLeft(handle))
        {
            var right = rect.Left + rect.Width;
            rect.Left = Math.Min(
                FrontedDesignerGeometryHelper.NormalizeCoordinate(
                    rect.Left,
                    effectiveSnapEnabled: true,
                    snapGridSize),
                right - FrontedDesignerGeometryHelper.MinResizeWidth);
            rect.Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, right - rect.Left);
            return;
        }

        if (AffectsRight(handle))
        {
            var right = FrontedDesignerGeometryHelper.NormalizeCoordinate(
                rect.Left + rect.Width,
                effectiveSnapEnabled: true,
                snapGridSize);
            rect.Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, right - rect.Left);
        }
    }

    private static void ApplyResizeYAxisSnap(
        ref ResizeRect rect,
        FrontedDesignerResizeHandleKind handle,
        double position)
    {
        if (AffectsTop(handle))
        {
            var bottom = rect.Top + rect.Height;
            rect.Top = Math.Min(position, bottom - FrontedDesignerGeometryHelper.MinResizeHeight);
            rect.Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, bottom - rect.Top);
        }
        else if (AffectsBottom(handle))
        {
            rect.Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, position - rect.Top);
        }
    }

    private static void NormalizeResizeYAxisFallback(
        ref ResizeRect rect,
        FrontedDesignerResizeHandleKind handle,
        double snapGridSize)
    {
        if (AffectsTop(handle))
        {
            var bottom = rect.Top + rect.Height;
            rect.Top = Math.Min(
                FrontedDesignerGeometryHelper.NormalizeCoordinate(
                    rect.Top,
                    effectiveSnapEnabled: true,
                    snapGridSize),
                bottom - FrontedDesignerGeometryHelper.MinResizeHeight);
            rect.Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, bottom - rect.Top);
            return;
        }

        if (AffectsBottom(handle))
        {
            var bottom = FrontedDesignerGeometryHelper.NormalizeCoordinate(
                rect.Top + rect.Height,
                effectiveSnapEnabled: true,
                snapGridSize);
            rect.Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, bottom - rect.Top);
        }
    }

    private static FrontedDesignerSnapGuide CreateGuide(
        FrontedDesignerSnapGuideOrientation orientation,
        double position,
        double extent,
        FrontedDesignerSnapGuideSource source,
        string? label)
    {
        return new FrontedDesignerSnapGuide
        {
            Orientation = orientation,
            Position = position,
            Start = 0D,
            End = Math.Max(0D, extent),
            Source = source,
            Label = label
        };
    }

    private static void AddCandidate(
        ICollection<SnapCandidate> candidates,
        double position,
        FrontedDesignerSnapGuideSource source,
        string? label)
    {
        if (double.IsFinite(position))
        {
            candidates.Add(new SnapCandidate(position, source, label));
        }
    }

    private static bool IsValidBounds(FrontedDesignerResolvedBounds bounds)
    {
        return double.IsFinite(bounds.Left)
               && double.IsFinite(bounds.Top)
               && double.IsFinite(bounds.Width)
               && double.IsFinite(bounds.Height)
               && bounds.Width > 0D
               && bounds.Height > 0D;
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

    private readonly record struct SnapCandidate(
        double Position,
        FrontedDesignerSnapGuideSource Source,
        string? Label);

    private readonly record struct SnapCandidateSet(
        IReadOnlyList<SnapCandidate> X,
        IReadOnlyList<SnapCandidate> Y);

    private readonly record struct SnapMatch(
        SnapCandidate Candidate,
        double Offset);

    private struct ResizeRect(
        double left,
        double top,
        double width,
        double height)
    {
        public double Left { get; set; } = left;

        public double Top { get; set; } = top;

        public double Width { get; set; } = width;

        public double Height { get; set; } = height;
    }
}
