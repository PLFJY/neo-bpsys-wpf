using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// Score System v2 中一局对大比分的贡献结果。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoreGameMajorResult
{
    /// <summary>
    /// 主场队赢得本局大分。
    /// </summary>
    HomeWin,

    /// <summary>
    /// 客场队赢得本局大分。
    /// </summary>
    AwayWin,

    /// <summary>
    /// 本局大分为平局。
    /// </summary>
    Tie
}
