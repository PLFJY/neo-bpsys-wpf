using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 地图
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Map
{
    NoBans,
    ArmsFactory,
    TheRedChurch,
    SacredHeartHospital,
    LeosMemory,
    MoonlitRiverPark,
    LakesideVillage,
    EversleepingTown,
    ChinaTown,
    Darkwoods
}