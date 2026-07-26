namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 CutScene 天赋/辅助特质业务控件配置。
/// </summary>
public class TalentTraitDisplayControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化天赋/辅助特质控件配置。
    /// </summary>
    public TalentTraitDisplayControlConfig()
    {
        ControlType = "TalentTraitDisplay";
    }

    /// <summary>
    /// 显示类型。
    /// </summary>
    public TalentTraitDisplayKind DisplayKind { get; set; }

    /// <summary>
    /// 求生者位置。仅 <see cref="TalentTraitDisplayKind.SurvivorTalent"/> 使用，合法值为 0..3。
    /// </summary>
    public int? PlayerIndex { get; set; }

    /// <summary>
    /// 图标宽高。
    /// </summary>
    public double IconSize { get; set; } = 38;

    /// <summary>
    /// 图标间距。
    /// </summary>
    public double IconGap { get; set; }

    /// <summary>
    /// 水平对齐。
    /// </summary>
    public string? HorizontalAlignment { get; set; }

    /// <summary>
    /// 垂直对齐。
    /// </summary>
    public string? VerticalAlignment { get; set; }

    /// <summary>
    /// 监管者辅助特质是否跟随共享的可见性状态。
    /// </summary>
    public bool RespectTraitVisibility { get; set; } = true;

    /// <summary>
    /// 求生者天赋配置是否提供了合法选手下标。
    /// </summary>
    public bool HasValidSurvivorPlayerIndex() =>
        DisplayKind != TalentTraitDisplayKind.SurvivorTalent
        || PlayerIndex is >= 0 and <= 3;
}
