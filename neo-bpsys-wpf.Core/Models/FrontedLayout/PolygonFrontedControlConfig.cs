using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public class PolygonFrontedControlConfig : ShapeFrontedControlConfigBase, IPolygonFrontedControlConfig
{
    public PolygonFrontedControlConfig()
    {
        ControlType = "Polygon";
    }

    private ObservableCollection<PolygonVertexConfig> _points = CreateDefaultPoints();

    public ObservableCollection<PolygonVertexConfig> Points
    {
        get => _points;
        set => _points = value ?? [];
    }

    public static ObservableCollection<PolygonVertexConfig> CreateDefaultPoints() =>
    [
        new(0.5, 0),
        new(1, 1),
        new(0, 1)
    ];
}

public class PolygonVertexConfig
{
    public PolygonVertexConfig()
    {
    }

    public PolygonVertexConfig(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; set; }

    public double Y { get; set; }
}
