using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 天赋类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TraitType
{
    传送,
    聆听,
    窥视者,
    闪现,
    失常,
    兴奋,
    巡视者,
    移形
}