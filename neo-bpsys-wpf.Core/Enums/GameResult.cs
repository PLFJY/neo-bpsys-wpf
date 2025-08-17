using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 对局结果
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameResult
{
    Escape4,
    Escape3,
    Tie,
    Out3,
    Out4
}