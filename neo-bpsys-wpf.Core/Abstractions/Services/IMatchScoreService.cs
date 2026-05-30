using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.ScoreSystem;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IMatchScoreService
{
    MatchScoreState Current { get; }

    ScoreHalf? CurrentHalf { get; }

    ScoreGame? CurrentGameScore { get; }

    ScoreHalf? GetHalf(GameProgress progress);

    ScoreGame? GetGame(GameProgress progress);

    void SetCurrentHalfResult(GameResult? result);

    void ClearCurrentHalfResult();

    void Recalculate();

    void RefreshCurrentProgress();

    void SyncLegacyTeamScoreMirror();
}
