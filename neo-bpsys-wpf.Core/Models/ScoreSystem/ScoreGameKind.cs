using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// Score System v2 中单局比分的类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoreGameKind
{
    /// <summary>
    /// 常规 BP 对局。
    /// </summary>
    Normal,

    /// <summary>
    /// 加赛局，例如 BO3 第 3 局加赛或 BO5 第 5 局加赛。
    /// </summary>
    Overtime
}
