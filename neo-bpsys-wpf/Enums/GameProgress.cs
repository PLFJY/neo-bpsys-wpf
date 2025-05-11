using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums;

/// <summary>
/// 表示比赛进行的不同阶段，用于跟踪系统当前所处的比赛流程状态
/// 包含常规比赛阶段和可能出现的加时赛阶段
/// 枚举值命名规则：Game{比赛序号}{半场阶段}或Game{比赛序号}Extra{半场阶段}，其中Extra表示加时赛阶段
/// </summary>
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