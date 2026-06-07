using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public sealed class BackgroundTintProcessingOptions
{
    public BackgroundTintMode Mode { get; init; }
    public double TintStrength { get; init; } = 1D;
    public double TextureStrength { get; init; } = 0.45D;
    public BackgroundTintNormalizationMode NormalizationMode { get; init; } =
        BackgroundTintNormalizationMode.VisibleMask;
    public Rect CanvasRegion { get; init; }
    public double CanvasWidth { get; init; }
    public double CanvasHeight { get; init; }
    public IReadOnlyList<PolygonVertexConfig>? PolygonPoints { get; init; }
}
