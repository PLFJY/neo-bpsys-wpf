using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 语言键，用于标识应用支持的语言。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LanguageKey
{
    /// <summary>系统默认语言</summary>
    System,
    /// <summary>跟随应用设置</summary>
    FollowApp,
    /// <summary>简体中文</summary>
    zh_Hans,
    /// <summary>英语（美国）</summary>
    en_US,
    /// <summary>日语</summary>
    ja_JP
}
