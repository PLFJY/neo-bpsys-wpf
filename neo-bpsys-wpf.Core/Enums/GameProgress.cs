using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 游戏进度枚举，Bo3加赛和Bo4共用枚举值
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameProgress
{
    /// <summary>空闲（未开始）</summary>
    Free = -1,
    /// <summary>第一局上半场</summary>
    Game1FirstHalf = 0,
    /// <summary>第一局下半场</summary>
    Game1SecondHalf = 1,
    /// <summary>第二局上半场</summary>
    Game2FirstHalf = 2,
    /// <summary>第二局下半场</summary>
    Game2SecondHalf = 3,
    /// <summary>第三局上半场</summary>
    Game3FirstHalf = 4,
    /// <summary>第三局下半场</summary>
    Game3SecondHalf = 5,
    /// <summary>
    /// 第四局上半场。代码上等价于<see cref="Game3OvertimeFirstHalf"/>
    /// </summary>
    Game4FirstHalf = 6,
    /// <summary>
    /// 第四局下半场。代码上等价于<see cref="Game3OvertimeSecondHalf"/>
    /// </summary>
    Game4SecondHalf = 7,
    /// <summary>
    /// 第三局加赛上半场。代码上等价于<see cref="Game4FirstHalf"/>
    /// </summary>
    Game3OvertimeFirstHalf = 6,
    /// <summary>
    /// 第三局加赛下半场。代码上等价于<see cref="Game4SecondHalf"/>
    /// </summary>
    Game3OvertimeSecondHalf = 7,
    /// <summary>第五局上半场</summary>
    Game5FirstHalf = 8,
    /// <summary>第五局下半场</summary>
    Game5SecondHalf = 9,
    /// <summary>第五局加赛上半场</summary>
    Game5OvertimeFirstHalf = 10,
    /// <summary>第五局加赛下半场</summary>
    Game5OvertimeSecondHalf = 11,
}
