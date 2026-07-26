using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 操作当前赛事 Score System v2 比分状态的服务。
/// </summary>
/// <remarks>
/// 服务本身不持有权威比分数据；权威状态来自 <see cref="ISharedDataService.CurrentGame"/> 的
/// <see cref="Game.MatchScore"/>。它负责按当前 <see cref="GameProgress"/>、BO3/BO5 模式和实时阵营映射，
/// 将半场赛果写入 v2 状态并刷新派生显示字段。
/// </remarks>
public interface IMatchScoreService
{
    /// <summary>
    /// 当前赛事的权威 Score System v2 状态。
    /// </summary>
    MatchScoreState Current { get; }

    /// <summary>
    /// 当前进度对应的半场比分；当进度不属于可记录比分的半场时为 <see langword="null"/>。
    /// </summary>
    ScoreHalf? CurrentHalf { get; }

    /// <summary>
    /// 当前进度对应的单局比分；当进度不属于已建模单局时为 <see langword="null"/>。
    /// </summary>
    ScoreGame? CurrentGameScore { get; }

    /// <summary>
    /// 获取指定进度对应的半场比分。
    /// </summary>
    /// <param name="progress">赛事进度。</param>
    /// <returns>匹配到的半场比分；无法映射时为 <see langword="null"/>。</returns>
    ScoreHalf? GetHalf(GameProgress progress);

    /// <summary>
    /// 获取指定进度对应的单局比分。
    /// </summary>
    /// <param name="progress">赛事进度。</param>
    /// <returns>匹配到的单局比分；无法映射时为 <see langword="null"/>。</returns>
    ScoreGame? GetGame(GameProgress progress);

    /// <summary>
    /// 为当前半场写入赛果，并记录写入时求生者方和监管者方对应的主客场。
    /// </summary>
    /// <param name="result">半场赛果；传入 <see langword="null"/> 时清除当前半场赛果和阵营记录。</param>
    void SetCurrentHalfResult(GameResult? result);

    /// <summary>
    /// 清除当前半场赛果和写入时的阵营记录。
    /// </summary>
    void ClearCurrentHalfResult();

    /// <summary>
    /// 根据所有已记录半场赛果重新计算小分、大分和当前显示文本。
    /// </summary>
    void Recalculate();

    /// <summary>
    /// 按当前赛事进度、阵营映射和 BO3/BO5 模式刷新当前半场显示文本。
    /// </summary>
    void RefreshCurrentProgress();
}
