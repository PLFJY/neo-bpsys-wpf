using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Camp
{
    Sur,
    Hun,
}
