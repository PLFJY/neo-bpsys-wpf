using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 天赋类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Camp
{
    Sur,
    Hun,
}
