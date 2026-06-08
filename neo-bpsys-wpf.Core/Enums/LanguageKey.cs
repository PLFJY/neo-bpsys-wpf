using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LanguageKey
{
    System,
    FollowApp,
    zh_Hans,
    en_US,
    ja_JP
}
