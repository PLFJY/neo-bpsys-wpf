using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Trait
    {
        传送,
        聆听,
        窥视者,
        闪现,
        失常,
        兴奋,
        巡视者,
        移形
    }
}
