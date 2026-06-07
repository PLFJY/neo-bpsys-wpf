using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public static class PolygonVertexGeometryHelper
{
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

    public static double ClampCoordinate(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0D, 1D) : 0D;

    private static double GetDimension(double? value) =>
        value is > 0 && double.IsFinite(value.Value) ? value.Value : 1D;
}
