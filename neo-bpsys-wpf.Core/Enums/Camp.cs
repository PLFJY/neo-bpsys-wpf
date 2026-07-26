using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 天赋类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Camp
{
    /// <summary>求生者阵营</summary>
    Sur,
    /// <summary>监管者阵营</summary>
    Hun,
}
