namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// Score System v2 中按 BO3/BO5 赛制判断比分单元可见性的工具。
/// </summary>
public static class ScoreGameVisibility
{
    /// <summary>
    /// 判断指定比分单元是否应在当前 BO 模式下参与显示和总分计算。
    /// </summary>
    /// <param name="key">比分单元的稳定 key。</param>
    /// <param name="isBo3Mode">是否为 BO3 模式。</param>
    /// <returns>如果该比分单元在当前 BO 模式下可见，则为 <see langword="true"/>。</returns>
    public static bool IsVisibleInBoMode(ScoreGameKey key, bool isBo3Mode)
    {
        if (isBo3Mode)
        {
            return key.GameNumber is 1 or 2
                   || key is { GameNumber: 3, GameKind: ScoreGameKind.Normal or ScoreGameKind.Overtime };
        }

        return key.GameKind == ScoreGameKind.Normal
               || key is { GameNumber: 5, GameKind: ScoreGameKind.Overtime };
    }
}
