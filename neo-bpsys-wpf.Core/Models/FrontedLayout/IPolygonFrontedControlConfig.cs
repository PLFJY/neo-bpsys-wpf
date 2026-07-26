using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 多边形控件配置接口。
/// </summary>
public interface IPolygonFrontedControlConfig
{
    /// <summary>
    /// 多边形顶点列表。
    /// </summary>
    ObservableCollection<PolygonVertexConfig> Points { get; set; }
}
