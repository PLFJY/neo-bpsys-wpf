using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoreHalfKind
{
    FirstHalf,
    SecondHalf
}
