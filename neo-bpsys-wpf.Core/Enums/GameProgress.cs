using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 游戏进度枚举，Bo3加赛和Bo4共用枚举值
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameProgress
{
    Free = -1,
    Game1FirstHalf = 0,
    Game1SecondHalf = 1,
    Game2FirstHalf = 2,
    Game2SecondHalf = 3,
    Game3FirstHalf = 4,
    Game3SecondHalf = 5,
    /// <summary>
    /// 代码上等价于<see cref="Game3ExtraFirstHalf"/>
    /// </summary>
    Game4FirstHalf = 6,
    /// <summary>
    /// 代码上等价于<see cref="Game3ExtraSecondHalf"/>
    /// </summary>
    Game4SecondHalf = 7,
    /// <summary>
    /// 代码上等价于<see cref="Game4FirstHalf"/>
    /// </summary>
    Game3ExtraFirstHalf = 6,
    /// <summary>
    /// 代码上等价于<see cref="Game4SecondHalf"/>
    /// </summary>
    Game3ExtraSecondHalf = 7,
    Game5FirstHalf = 8,
    Game5SecondHalf = 9,
    Game5ExtraFirstHalf = 10,
    Game5ExtraSecondHalf = 11,
}
