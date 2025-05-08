using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameProgress
{
    Free,
    Game1FirstHalf,
    Game1SecondHalf,
    Game2FirstHalf,
    Game2SecondHalf,
    Game3FirstHalf,
    Game3SecondHalf,
    Game3ExtraFirstHalf,
    Game3ExtraSecondHalf,
    Game4FirstHalf,
    Game4SecondHalf,
    Game5FirstHalf,
    Game5SecondHalf,
    Game5ExtraFirstHalf,
    Game5ExtraSecondHalf,
}
