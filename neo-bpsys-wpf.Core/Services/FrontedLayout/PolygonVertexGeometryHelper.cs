using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 多边形顶点几何辅助类，用于在归一化坐标和画布绝对坐标之间进行转换。
/// </summary>
public static class PolygonVertexGeometryHelper
{
    /// <summary>
    /// 将归一化的顶点坐标转换为画布上的绝对坐标。
    /// </summary>
    /// <param name="config">控件配置，用于获取位置和尺寸。</param>
    /// <param name="vertex">归一化顶点坐标（X/Y 在 0 到 1 之间）。</param>
    /// <returns>画布上的绝对坐标点。</returns>
    public static Point ToCanvasPoint(
        FrontedControlConfigBase config,
        PolygonVertexConfig vertex)
    {
        var width = GetDimension(config.Width);
        var height = GetDimension(config.Height);
        return new Point(
            config.Left + ClampCoordinate(vertex.X) * width,
            config.Top + ClampCoordinate(vertex.Y) * height);
    }

    /// <summary>
    /// 将画布上的绝对坐标点转换为归一化的顶点坐标。
    /// </summary>
    /// <param name="config">控件配置，用于获取位置和尺寸。</param>
    /// <param name="canvasPoint">画布上的绝对坐标点。</param>
    /// <returns>归一化顶点坐标（X/Y 在 0 到 1 之间）。</returns>
    public static PolygonVertexConfig ToNormalizedPoint(
        FrontedControlConfigBase config,
        Point canvasPoint)
    {
        var width = GetDimension(config.Width);
        var height = GetDimension(config.Height);
        return new PolygonVertexConfig(
            ClampCoordinate((canvasPoint.X - config.Left) / width),
            ClampCoordinate((canvasPoint.Y - config.Top) / height));
    }

    /// <summary>
    /// 将坐标值限制在 [0, 1] 范围内。非有限值返回 0。
    /// </summary>
    /// <param name="value">待限制的坐标值。</param>
    /// <returns>限制后的坐标值。</returns>
    public static double ClampCoordinate(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0D, 1D) : 0D;

    /// <summary>
    /// 根据顶点配置列表创建 WPF PointCollection，用于多边形绘制。
    /// </summary>
    /// <param name="config">控件配置，用于获取位置和尺寸。</param>
    /// <param name="vertices">顶点配置列表。若数量不足 3 个，将使用默认三角形顶点。</param>
    /// <returns>WPF PointCollection。</returns>
    public static PointCollection CreateLocalPointCollection(
        FrontedControlConfigBase config,
        IEnumerable<PolygonVertexConfig>? vertices)
    {
        var points = vertices?
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
        if (points is not { Length: >= 3 })
        {
            points = [.. PolygonFrontedControlConfig.CreateDefaultPoints()];
        }

        var width = GetDimension(config.Width);
        var height = GetDimension(config.Height);
        return new PointCollection(points.Select(point => new Point(
            ClampCoordinate(point.X) * width,
            ClampCoordinate(point.Y) * height)));
    }

    private static double GetDimension(double? value) =>
        value is > 0 && double.IsFinite(value.Value) ? value.Value : 1D;
}
