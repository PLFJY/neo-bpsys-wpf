using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 多边形控件配置。
/// </summary>
public class PolygonFrontedControlConfig : ShapeFrontedControlConfigBase, IPolygonFrontedControlConfig
{
    /// <summary>
    /// 初始化多边形控件配置。
    /// </summary>
    public PolygonFrontedControlConfig()
    {
        ControlType = "Polygon";
    }

    private ObservableCollection<PolygonVertexConfig> _points = CreateDefaultPoints();

    /// <summary>
    /// 多边形顶点列表。
    /// </summary>
    public ObservableCollection<PolygonVertexConfig> Points
    {
        get => _points;
        set => _points = value ?? [];
    }

    /// <summary>
    /// 创建默认顶点列表。
    /// </summary>
    /// <returns>默认顶点列表。</returns>
    public static ObservableCollection<PolygonVertexConfig> CreateDefaultPoints() =>
    [
        new(0.5, 0),
        new(1, 1),
        new(0, 1)
    ];
}

/// <summary>
/// 多边形顶点配置。
/// </summary>
public class PolygonVertexConfig
{
    /// <summary>
    /// 无参构造函数。
    /// </summary>
    public PolygonVertexConfig()
    {
    }

    /// <summary>
    /// 使用指定坐标构造顶点。
    /// </summary>
    /// <param name="x">X 坐标。</param>
    /// <param name="y">Y 坐标。</param>
    public PolygonVertexConfig(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// X 坐标。
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y 坐标。
    /// </summary>
    public double Y { get; set; }
}
