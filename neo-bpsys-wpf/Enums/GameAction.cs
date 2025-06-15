using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
// 用于存放当前对局步骤
public enum GameAction
{
    None, // 空（默认值）
    BanMap, // ban图
    PickMap, // 选图
    PickCamp, // 选阵营
    BanSur, // 以下略
    BanHun,
    PickSur,
    PickHun,
    PickSurTalent,
    PickHunTalent,
    DistributeChara,
    EndGuidance
}
