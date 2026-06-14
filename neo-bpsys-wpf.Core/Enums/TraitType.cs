using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 天赋类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TraitType
{
    /// <summary>传送</summary>
    Teleport,
    /// <summary>聆听</summary>
    Listen,
    /// <summary>窥视者</summary>
    Peeper,
    /// <summary>闪现</summary>
    Blink,
    /// <summary>失常</summary>
    Abnormal,
    /// <summary>兴奋</summary>
    Excitement,
    /// <summary>巡视者</summary>
    Patroller,
    /// <summary>移形</summary>
    Warp
}