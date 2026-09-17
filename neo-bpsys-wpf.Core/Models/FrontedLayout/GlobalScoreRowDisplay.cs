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
    double Top,
    double Width,
    double Height,
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

        foreach (var game in matchScore.Games.Where(game => ScoreGameVisibility.IsVisibleInBoMode(game.Key, isBo3Mode)))
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
                0,
                75,
                32,
                displayScore?.ToString(CultureInfo.InvariantCulture) ?? "-",
                showCampIcon && displayScore.HasValue && camp != null,
                camp is Camp.Hun));
        }
    }

    /// <summary>
    /// 根据显式 cell 配置解析单个比分格显示数据。
    /// </summary>
    public static GlobalScoreRowCellDisplay Create(
        MatchScoreState matchScore,
        TeamType teamType,
        GlobalScoreCellConfig cell,
        bool showCampIcon)
    {
        var gameKey = new ScoreGameKey(cell.GameNumber, cell.GameKind);
        var half = matchScore.Games
            .FirstOrDefault(game => game.Key == gameKey) is { } game
            ? cell.HalfKind == ScoreHalfKind.FirstHalf ? game.FirstHalf : game.SecondHalf
            : null;

        if (half is null || half.Result is null)
        {
            return new GlobalScoreRowCellDisplay(
                gameKey,
                cell.HalfKind,
                cell.X,
                cell.Y,
                cell.Width,
                cell.Height,
                "-",
                false,
                false);
        }

        var camp = GetRecordedCamp(half, teamType);
        var score = teamType == TeamType.HomeTeam ? half.HomeMinorScore : half.AwayMinorScore;
        return new GlobalScoreRowCellDisplay(
            gameKey,
            cell.HalfKind,
            cell.X,
            cell.Y,
            cell.Width,
            cell.Height,
            score?.ToString(CultureInfo.InvariantCulture) ?? "-",
            showCampIcon && score.HasValue && camp != null,
            camp is Camp.Hun);
    }

    /// <summary>
    /// 根据显式 cell 配置解析自由对局的单个比分格显示数据。
    /// </summary>
    /// <param name="freeScore">自由对局比分状态。</param>
    /// <param name="teamType">要显示的主队或客队。</param>
    /// <param name="cell">比分格布局配置。</param>
    /// <param name="showCampIcon">是否显示阵营图标。</param>
    /// <returns>自由对局比分格显示数据。</returns>
    public static GlobalScoreRowCellDisplay Create(
        FreeMatchScoreState freeScore,
        TeamType teamType,
        GlobalScoreCellConfig cell,
        bool showCampIcon)
    {
        var gameKey = new ScoreGameKey(cell.GameNumber, cell.GameKind);
        var half = freeScore.GetGlobalHalf(gameKey, cell.HalfKind);
        if (half is null || !half.IsCompleted)
        {
            return new GlobalScoreRowCellDisplay(
                gameKey,
                cell.HalfKind,
                cell.X,
                cell.Y,
                cell.Width,
                cell.Height,
                "-",
                false,
                false);
        }

        var score = teamType == TeamType.HomeTeam ? half.HomeMinorScore : half.AwayMinorScore;
        var camp = teamType == TeamType.HomeTeam ? half.HomeCamp : half.AwayCamp;
        return new GlobalScoreRowCellDisplay(
            gameKey,
            cell.HalfKind,
            cell.X,
            cell.Y,
            cell.Width,
            cell.Height,
            score.ToString(CultureInfo.InvariantCulture),
            showCampIcon,
            camp == Camp.Hun);
    }

    /// <summary>
    /// 为旧 gap-only 配置生成兼容 cell 列表。
    /// </summary>
    public static List<GlobalScoreCellConfig> CreateDefaultCells(
        MatchScoreState matchScore,
        bool isBo3Mode,
        double majorGameGap,
        double halfGameGap) =>
        Services.FrontedLayout.GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(
            majorGameGap,
            halfGameGap,
            isBo3Mode);

    /// <summary>
    /// 显式 BO3/BO5 可见性规则，避免依赖 GameProgress enum 原始数值。
    /// </summary>
    public static bool IsVisibleInBoMode(ScoreGameKey key, bool isBo3Mode) =>
        ScoreGameVisibility.IsVisibleInBoMode(key, isBo3Mode);

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
