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

    /// <summary>
    /// 是否只处理 <see cref="CanvasRegion"/> 覆盖的像素区域。
    /// </summary>
    /// <remarks>
    /// 仅对 <see cref="BackgroundTintMode.BaseColorWithTexture"/> 生效：该模式下结果依赖可见区域的
    /// 局部平均亮度，区域以外的像素会被宿主裁剪、不可见，因此可以跳过计算，区域外保留源图像素。
    /// 默认关闭，保持直接调用处理器的完整图像语义；运行时宿主开启以降低 CPU 开销。
    /// </remarks>
    public bool RestrictProcessingToRegion { get; init; }
}
