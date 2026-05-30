using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// CutScene v3 天赋/辅助特质显示类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TalentTraitDisplayKind
{
    /// <summary>
    /// 求生者天赋。
    /// </summary>
    SurvivorTalent,

    /// <summary>
    /// 监管者天赋。
    /// </summary>
    HunterTalent,

    /// <summary>
    /// 监管者辅助特质。
    /// </summary>
    HunterTrait
}
