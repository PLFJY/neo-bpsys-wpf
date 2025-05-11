using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums
{
    /// <summary>
    /// 地图枚举
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Map
    {
        军工厂,
        红教堂,
        圣心医院,
        湖景村,
        月亮河公园,
        里奥的回忆,
        唐人街,
        永眠镇,
        不归林,
        无禁用
    }
}
