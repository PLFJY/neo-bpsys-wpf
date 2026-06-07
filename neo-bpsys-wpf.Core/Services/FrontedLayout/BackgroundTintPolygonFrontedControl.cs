using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public class BackgroundTintPolygonFrontedControl(BackgroundImageTintProcessor processor) : IFrontedControl
{
    public BackgroundTintPolygonFrontedControl()
        : this(new BackgroundImageTintProcessor())
    {
    }

    public string ControlType => "BackgroundTintPolygon";

    public Type ConfigType => typeof(BackgroundTintPolygonFrontedControlConfig);

    public FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context)
    {
        if (config is not BackgroundTintPolygonFrontedControlConfig polygon)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a BackgroundTintPolygon config.");
        }

        return BackgroundTintFrontedControlFactoryHelper.Create(
            name,
            polygon,
            context,
            processor,
            root => CreateGeometry(polygon, root, context.Logger),
            BackgroundTintNormalizationMode.VisiblePolygon,
            polygon.Points);
    }

    public static PathGeometry CreateGeometry(
        BackgroundTintPolygonFrontedControlConfig config,
        FrameworkElement? element = null,
        ILogger? logger = null)
    {
        var validPoints = config.Points?
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
        if (validPoints is not { Length: >= 3 })
        {
            logger?.LogWarning("Background tint polygon points are invalid; using default triangle.");
            validPoints = [.. PolygonFrontedControlConfig.CreateDefaultPoints()];
        }

        var width = element is null
            ? config.Width ?? 1D
            : BackgroundTintFrontedControlFactoryHelper.GetWidth(element, config);
        var height = element is null
            ? config.Height ?? 1D
            : BackgroundTintFrontedControlFactoryHelper.GetHeight(element, config);
        var points = validPoints.Select(point => new Point(
            PolygonVertexGeometryHelper.ClampCoordinate(point.X) * width,
            PolygonVertexGeometryHelper.ClampCoordinate(point.Y) * height)).ToArray();
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        return new PathGeometry([figure]);
    }
}
