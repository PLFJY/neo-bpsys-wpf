using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Enums
{
    /// <summary>
    ///  监管者辅助特质枚举
    /// </summary>
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
