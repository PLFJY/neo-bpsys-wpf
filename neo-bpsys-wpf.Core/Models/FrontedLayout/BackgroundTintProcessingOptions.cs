using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 背景染色处理选项。
/// </summary>
public sealed class BackgroundTintProcessingOptions
{
    /// <summary>
    /// 染色模式。
    /// </summary>
    public BackgroundTintMode Mode { get; init; }

    /// <summary>
    /// 染色强度。
    /// </summary>
    public double TintStrength { get; init; } = 1D;

    /// <summary>
    /// 纹理强度。
    /// </summary>
    public double TextureStrength { get; init; } = 0.45D;

    /// <summary>
    /// 归一化模式。
    /// </summary>
    public BackgroundTintNormalizationMode NormalizationMode { get; init; } =
        BackgroundTintNormalizationMode.VisibleMask;

    /// <summary>
    /// Canvas 区域。
    /// </summary>
    public Rect CanvasRegion { get; init; }

    /// <summary>
    /// Canvas 宽度。
    /// </summary>
    public double CanvasWidth { get; init; }

    /// <summary>
    /// Canvas 高度。
    /// </summary>
    public double CanvasHeight { get; init; }

    /// <summary>
    /// 多边形顶点列表（可选）。
    /// </summary>
    public IReadOnlyList<PolygonVertexConfig>? PolygonPoints { get; init; }
}
