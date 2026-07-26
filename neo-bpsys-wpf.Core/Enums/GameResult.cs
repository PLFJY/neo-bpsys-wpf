using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 对局结果
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameResult
{
    /// <summary>四出（求生者四人逃脱）</summary>
    Escape4,
    /// <summary>三出（求生者三人逃脱）</summary>
    Escape3,
    /// <summary>平局</summary>
    Tie,
    /// <summary>三杀（监管者淘汰三人）</summary>
    Out3,
    /// <summary>四杀（监管者淘汰四人）</summary>
    Out4
}