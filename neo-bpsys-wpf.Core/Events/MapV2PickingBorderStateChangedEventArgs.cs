namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 地图 BP v2 选图边框状态改变事件参数。
/// </summary>
/// <param name="mapKey">地图字典 key。</param>
/// <param name="isMapV2Breathing">地图 BP v2 呼吸灯总开关是否开启。</param>
/// <param name="isMapBanned">地图是否已被禁用。</param>
public sealed class MapV2PickingBorderStateChangedEventArgs(
    string mapKey,
    bool isMapV2Breathing,
    bool isMapBanned) : EventArgs
{
    /// <summary>
    /// 获取地图字典 key。
    /// </summary>
    public string MapKey { get; } = mapKey;

    /// <summary>
    /// 获取地图 BP v2 呼吸灯总开关是否开启。
    /// </summary>
    public bool IsMapV2Breathing { get; } = isMapV2Breathing;

    /// <summary>
    /// 获取地图是否已被禁用。
    /// </summary>
    public bool IsMapBanned { get; } = isMapBanned;

    /// <summary>
    /// 获取选图边框是否应显示。
    /// </summary>
    public bool IsPickingBorderVisible => IsMapV2Breathing && !IsMapBanned;
}
