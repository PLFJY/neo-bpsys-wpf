using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 步骤引导步骤行为
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameAction
{
    /// <summary>空（默认值）</summary>
    None,
    /// <summary>Ban地图</summary>
    BanMap,
    /// <summary>选择地图</summary>
    PickMap,
    /// <summary>选择阵营</summary>
    PickCamp,
    /// <summary>Ban求生者</summary>
    BanSur,
    /// <summary>Ban监管者</summary>
    BanHun,
    /// <summary>选择求生者</summary>
    PickSur,
    /// <summary>选择监管者</summary>
    PickHun,
    /// <summary>求生者选择天赋</summary>
    PickSurTalent,
    /// <summary>监管者选择天赋</summary>
    PickHunTalent,
    /// <summary>分配角色</summary>
    DistributeChara,
    /// <summary>结束引导</summary>
    EndGuidance
}