using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public class BackgroundTintPolygonFrontedControlConfig : BackgroundTintFrontedControlConfigBase, IPolygonFrontedControlConfig
{
    public BackgroundTintPolygonFrontedControlConfig()
    {
        ControlType = "BackgroundTintPolygon";
    }

    private ObservableCollection<PolygonVertexConfig> _points = PolygonFrontedControlConfig.CreateDefaultPoints();

    public ObservableCollection<PolygonVertexConfig> Points
    {
        get => _points;
        set => _points = value ?? [];
    }
}
