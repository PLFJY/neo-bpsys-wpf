using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// Score System v2 中单局的上下半场标识。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoreHalfKind
{
    /// <summary>
    /// 上半场。
    /// </summary>
    FirstHalf,

    /// <summary>
    /// 下半场。
    /// </summary>
    SecondHalf
}
