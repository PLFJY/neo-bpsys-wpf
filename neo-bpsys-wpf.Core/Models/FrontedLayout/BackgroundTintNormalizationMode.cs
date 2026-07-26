namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 背景染色归一化模式。
/// </summary>
public enum BackgroundTintNormalizationMode
{
    /// <summary>
    /// 使用整张图像进行归一化。
    /// </summary>
    WholeImage,

    /// <summary>
    /// 仅使用可见矩形区域进行归一化。
    /// </summary>
    VisibleRectangle,

    /// <summary>
    /// 仅使用可见多边形区域进行归一化。
    /// </summary>
    VisiblePolygon,

    /// <summary>
    /// 仅使用可见遮罩区域进行归一化。
    /// </summary>
    VisibleMask
}
