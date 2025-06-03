using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GameResult
    {
        Escape4,
        Escape3,
        Tie,
        Out3,
        Out4
    }
}
