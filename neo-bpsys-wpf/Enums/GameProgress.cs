using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1069:不应复制枚举值", Justification = "<挂起>")] //BO3加赛和BO4是共用控件和字典值的
[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:请删除不必要的忽略", Justification = "<挂起>")]
public enum GameProgress
{
    Free = -1,
    Game1FirstHalf = 0,
    Game1SecondHalf = 1,
    Game2FirstHalf = 2,
    Game2SecondHalf = 3,
    Game3FirstHalf = 4,
    Game3SecondHalf = 5,
    Game4FirstHalf = 6,
    Game4SecondHalf = 7,
    Game3ExtraFirstHalf = 6,
    Game3ExtraSecondHalf = 7,
    Game5FirstHalf = 8,
    Game5SecondHalf = 9,
    Game5ExtraFirstHalf = 10,
    Game5ExtraSecondHalf = 11,
}
