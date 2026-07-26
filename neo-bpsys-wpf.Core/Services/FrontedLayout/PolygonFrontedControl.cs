using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.PluginSdk;
using System.Windows;
using System.Windows.Media;
using Polygon = System.Windows.Shapes.Polygon;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 多边形控件。
/// </summary>
[FrontedV3Control("Polygon", IsBuiltIn = true)]
public class PolygonFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not PolygonFrontedControlConfig polygonConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a Polygon config.");
        }

        var buildContext = context.ToBuildContext();
        var polygon = new Polygon
        {
            Name = context.ControlName,
            Points = CreatePointCollection(polygonConfig, context.Logger)
        };
        ShapeFillBrushFactory.Apply(polygon, polygonConfig, buildContext);
        Content = polygon;
    }

    /// <summary>
    /// 根据配置创建多边形顶点集合。
    /// </summary>
    /// <param name="config">多边形控件配置。</param>
    /// <returns>顶点集合。</returns>
    public static PointCollection CreatePointCollection(PolygonFrontedControlConfig config) =>
        CreatePointCollection(config, null);

    private static PointCollection CreatePointCollection(
        PolygonFrontedControlConfig config,
        ILogger? logger)
    {
        var points = config.Points?
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
        if (points is not { Length: >= 3 })
        {
            logger?.LogWarning("Polygon points are invalid; using default triangle.");
            points = [.. PolygonFrontedControlConfig.CreateDefaultPoints()];
        }

        return PolygonVertexGeometryHelper.CreateLocalPointCollection(config, points);
    }
}
