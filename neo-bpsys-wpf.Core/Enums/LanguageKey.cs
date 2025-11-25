using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LanguageKey
{
    System,
    zh_Hans,
    en_US,
    ja_JP
}
