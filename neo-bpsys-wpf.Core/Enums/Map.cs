using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 地图
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Map
{
    无禁用,
    军工厂,
    红教堂,
    圣心医院,
    里奥的回忆,
    月亮河公园,
    湖景村,
    永眠镇,
    唐人街,
    不归林
}