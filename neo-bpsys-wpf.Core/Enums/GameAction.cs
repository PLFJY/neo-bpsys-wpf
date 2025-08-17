using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 步骤引导步骤行为
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameAction
{
    None, // 空（默认值）
    BanMap, // ban图
    PickMap, // 选图
    PickCamp, // 选阵营
    BanSur, // Ban求生
    BanHun, // Ban监管
    PickSur, // 选求生
    PickHun, // 选监管
    PickSurTalent, // 求生者选择天赋
    PickHunTalent, // 监管者选择天赋
    DistributeChara, // 分配角色
    EndGuidance //结束引导
}