namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// 唯一定位 Score System v2 中一局比分的键。
/// </summary>
/// <param name="GameNumber">对局编号，通常对应第五人格赛事中的第 1 至第 5 局。</param>
/// <param name="GameKind">普通局或加赛局。</param>
public readonly record struct ScoreGameKey(int GameNumber, ScoreGameKind GameKind);
