using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 背景色调多边形控件。
/// </summary>
[FrontedV3Control("BackgroundTintPolygon", IsBuiltIn = true)]
public class BackgroundTintPolygonFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not BackgroundTintPolygonFrontedControlConfig polygon)
        {
            throw new FrontedLayoutConfigException("Control config is not a BackgroundTintPolygon config.");
        }

        var buildContext = context.ToBuildContext();
        var processor = context.Services.GetRequiredService<BackgroundImageTintProcessor>();
        var root = BackgroundTintFrontedControlFactoryHelper.Create(
            context.ControlName ?? string.Empty,
            polygon,
            buildContext,
            processor,
            element => CreateGeometry(polygon, element, context.Logger),
            BackgroundTintNormalizationMode.VisiblePolygon,
            polygon.Points);
        Content = root;
    }

    /// <summary>
    /// 根据配置创建多边形裁剪几何。
    /// </summary>
    /// <param name="config">背景色调多边形配置。</param>
    /// <param name="element">关联的可视元素，用于获取实际尺寸。</param>
    /// <param name="logger">可选日志。</param>
    /// <returns>多边形路径几何。</returns>
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
