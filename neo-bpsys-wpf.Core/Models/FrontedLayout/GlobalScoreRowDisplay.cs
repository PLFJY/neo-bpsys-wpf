using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.Globalization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 全局比分行中单个半场比分格的显示数据。
/// </summary>
public readonly record struct GlobalScoreRowCellDisplay(
    ScoreGameKey GameKey,
    ScoreHalfKind HalfKind,
    double Left,
    string Text,
    bool IsCampVisible,
    bool IsHunIcon);

/// <summary>
/// 全局比分行显示数据生成器。
/// </summary>
public static class GlobalScoreRowDisplay
{
    /// <summary>
    /// 按 Score System v2 的显式 ScoreGame 顺序生成全局比分行显示数据。
    /// </summary>
    public static IReadOnlyList<GlobalScoreRowCellDisplay> Create(
        MatchScoreState matchScore,
        TeamType teamType,
        bool isBo3Mode,
        double majorGameGap,
        double halfGameGap,
        bool showCampIcon)
    {
        var cells = new List<GlobalScoreRowCellDisplay>();
        var visibleGameIndex = 0;

        foreach (var game in matchScore.Games.Where(game => IsVisibleInBoMode(game.Key, isBo3Mode)))
        {
            AddHalf(game.Key, game.FirstHalf, visibleGameIndex, halfIndex: 0);
            AddHalf(game.Key, game.SecondHalf, visibleGameIndex, halfIndex: 1);
            visibleGameIndex++;
        }

        return cells;

        void AddHalf(ScoreGameKey key, ScoreHalf half, int gameIndex, int halfIndex)
        {
            var camp = GetRecordedCamp(half, teamType);
            var score = teamType == TeamType.HomeTeam ? half.HomeMinorScore : half.AwayMinorScore;
            int? displayScore = half.Result != null ? score : null;

            cells.Add(new GlobalScoreRowCellDisplay(
                key,
                half.HalfKind,
                gameIndex * majorGameGap + halfIndex * halfGameGap,
                displayScore?.ToString(CultureInfo.InvariantCulture) ?? "-",
                showCampIcon && displayScore.HasValue && camp != null,
                camp is Camp.Hun));
        }
    }

    /// <summary>
    /// 显式 BO3/BO5 可见性规则，避免依赖 GameProgress enum 原始数值。
    /// </summary>
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

    private static Camp? GetRecordedCamp(ScoreHalf half, TeamType teamType)
    {
        if (half.SurTeamTypeWhenRecorded == teamType)
        {
            return Camp.Sur;
        }

        if (half.HunTeamTypeWhenRecorded == teamType)
        {
            return Camp.Hun;
        }

        return null;
    }
}
