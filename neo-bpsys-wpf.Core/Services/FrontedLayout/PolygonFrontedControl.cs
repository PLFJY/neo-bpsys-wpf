using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Media;
using Polygon = System.Windows.Shapes.Polygon;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public class PolygonFrontedControl : IFrontedControl
{
    public string ControlType => "Polygon";

    public Type ConfigType => typeof(PolygonFrontedControlConfig);

    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not PolygonFrontedControlConfig polygonConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a Polygon config.");
        }

        var polygon = new Polygon
        {
            Name = name,
            Points = CreatePointCollection(polygonConfig, context)
        };
        FrontedControlFactoryHelper.ApplyCanvasLayout(polygon, polygonConfig);
        ShapeFillBrushFactory.Apply(polygon, polygonConfig, context);
        return polygon;
    }

    public static PointCollection CreatePointCollection(PolygonFrontedControlConfig config) =>
        CreatePointCollection(config, null);

    private static PointCollection CreatePointCollection(
        PolygonFrontedControlConfig config,
        FrontedControlBuildContext? context)
    {
        var points = config.Points?
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
        if (points is not { Length: >= 3 })
        {
            context?.Logger?.LogWarning("Polygon points are invalid; using default triangle.");
            points = [.. PolygonFrontedControlConfig.CreateDefaultPoints()];
        }

        return PolygonVertexGeometryHelper.CreateLocalPointCollection(config, points);
    }
}
