using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 背景染色多边形控件配置。
/// </summary>
public class BackgroundTintPolygonFrontedControlConfig : BackgroundTintFrontedControlConfigBase, IPolygonFrontedControlConfig
{
    /// <summary>
    /// 初始化背景染色多边形控件配置。
    /// </summary>
    public BackgroundTintPolygonFrontedControlConfig()
    {
        ControlType = "BackgroundTintPolygon";
    }

    private ObservableCollection<PolygonVertexConfig> _points = PolygonFrontedControlConfig.CreateDefaultPoints();

    /// <summary>
    /// 多边形顶点列表。
    /// </summary>
    public ObservableCollection<PolygonVertexConfig> Points
    {
        get => _points;
        set => _points = value ?? [];
    }
}
