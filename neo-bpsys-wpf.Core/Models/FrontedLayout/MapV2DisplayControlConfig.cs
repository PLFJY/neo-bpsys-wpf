namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 地图 BP v2 展示控件配置。
/// </summary>
public class MapV2DisplayControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化地图 BP v2 展示控件配置。
    /// </summary>
    public MapV2DisplayControlConfig()
    {
        ControlType = "MapV2Display";
    }

    /// <summary>
    /// 地图字典 key。
    /// </summary>
    public string MapKey { get; set; } = string.Empty;
}
