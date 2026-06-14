using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 地图
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Map
{
    /// <summary>无Ban</summary>
    NoBans,
    /// <summary>军工厂</summary>
    ArmsFactory,
    /// <summary>红教堂</summary>
    TheRedChurch,
    /// <summary>圣心医院</summary>
    SacredHeartHospital,
    /// <summary>里奥的回忆</summary>
    LeosMemory,
    /// <summary>月亮河公园</summary>
    MoonlitRiverPark,
    /// <summary>湖景村</summary>
    LakesideVillage,
    /// <summary>永眠镇</summary>
    EversleepingTown,
    /// <summary>唐人街</summary>
    ChinaTown,
    /// <summary>不归林</summary>
    Darkwoods
}