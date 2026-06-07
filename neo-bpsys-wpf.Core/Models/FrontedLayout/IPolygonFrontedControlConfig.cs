using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public interface IPolygonFrontedControlConfig
{
    ObservableCollection<PolygonVertexConfig> Points { get; set; }
}
