using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class GlobalScoreRowDisplayTest
{
    [Fact]
    public void NullHalfResultDisplaysBarAndHidesCampIcon()
    {
        var matchScore = MatchScoreState.CreateDefault();

        var cells = GlobalScoreRowDisplay.Create(
            matchScore,
            TeamType.HomeTeam,
            isBo3Mode: false,
            majorGameGap: 180,
            halfGameGap: 90,
            showCampIcon: true);

        var firstCell = cells[0];
        Assert.Equal("-", firstCell.Text);
        Assert.False(firstCell.IsCampVisible);
    }

    [Fact]
    public void RecordedHalfDisplaysTeamScoreAndRecordedCamp()
    {
        var matchScore = MatchScoreState.CreateDefault();
        var half = matchScore.Games[0].FirstHalf;
        half.Result = GameResult.Escape3;
        half.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        half.HunTeamTypeWhenRecorded = TeamType.AwayTeam;

        var cells = GlobalScoreRowDisplay.Create(
            matchScore,
            TeamType.HomeTeam,
            isBo3Mode: false,
            majorGameGap: 180,
            halfGameGap: 90,
            showCampIcon: true);

        var firstCell = cells[0];
        Assert.Equal("3", firstCell.Text);
        Assert.True(firstCell.IsCampVisible);
        Assert.False(firstCell.IsHunIcon);
    }

    [Fact]
    public void ExplicitCellResolvesSecondHalfOvertimeMissingAndNullResult()
    {
        var matchScore = MatchScoreState.CreateDefault();
        var game1 = matchScore.Games.Single(game => game.Key == new ScoreGameKey(1, ScoreGameKind.Normal));
        game1.FirstHalf.Result = GameResult.Escape3;
        game1.FirstHalf.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        game1.FirstHalf.HunTeamTypeWhenRecorded = TeamType.AwayTeam;
        game1.SecondHalf.Result = GameResult.Out3;
        game1.SecondHalf.SurTeamTypeWhenRecorded = TeamType.AwayTeam;
        game1.SecondHalf.HunTeamTypeWhenRecorded = TeamType.HomeTeam;

        var overtime = matchScore.Games.Single(game => game.Key == new ScoreGameKey(5, ScoreGameKind.Overtime));
        overtime.FirstHalf.Result = GameResult.Tie;
        overtime.FirstHalf.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        overtime.FirstHalf.HunTeamTypeWhenRecorded = TeamType.AwayTeam;

        var firstHalf = GlobalScoreRowDisplay.Create(
            matchScore,
            TeamType.HomeTeam,
            Cell(1, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
            showCampIcon: true);
        var secondHalf = GlobalScoreRowDisplay.Create(
            matchScore,
            TeamType.HomeTeam,
            Cell(1, ScoreGameKind.Normal, ScoreHalfKind.SecondHalf),
            showCampIcon: true);
        var overtimeHalf = GlobalScoreRowDisplay.Create(
            matchScore,
            TeamType.HomeTeam,
            Cell(5, ScoreGameKind.Overtime, ScoreHalfKind.FirstHalf),
            showCampIcon: true);
        var missingHalf = GlobalScoreRowDisplay.Create(
            matchScore,
            TeamType.HomeTeam,
            Cell(9, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
            showCampIcon: true);
        var nullResult = GlobalScoreRowDisplay.Create(
            matchScore,
            TeamType.HomeTeam,
            Cell(2, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
            showCampIcon: true);

        Assert.Equal("3", firstHalf.Text);
        Assert.True(firstHalf.IsCampVisible);
        Assert.False(firstHalf.IsHunIcon);
        Assert.Equal("3", secondHalf.Text);
        Assert.True(secondHalf.IsCampVisible);
        Assert.True(secondHalf.IsHunIcon);
        Assert.Equal("2", overtimeHalf.Text);
        Assert.Equal("-", missingHalf.Text);
        Assert.Equal("-", nullResult.Text);
    }

    [Fact]
    public void Bo3AndBo5VisibilityUsesScoreGameKeysInsteadOfRawProgressValues()
    {
        var matchScore = MatchScoreState.CreateDefault();

        var bo3Keys = GlobalScoreRowDisplay.Create(
                matchScore,
                TeamType.HomeTeam,
                isBo3Mode: true,
                majorGameGap: 180,
                halfGameGap: 90,
                showCampIcon: true)
            .Select(cell => cell.GameKey)
            .Distinct()
            .ToList();
        var bo5Keys = GlobalScoreRowDisplay.Create(
                matchScore,
                TeamType.HomeTeam,
                isBo3Mode: false,
                majorGameGap: 180,
                halfGameGap: 90,
                showCampIcon: true)
            .Select(cell => cell.GameKey)
            .Distinct()
            .ToList();

        Assert.Contains(new ScoreGameKey(3, ScoreGameKind.Overtime), bo3Keys);
        Assert.DoesNotContain(new ScoreGameKey(4, ScoreGameKind.Normal), bo3Keys);
        Assert.DoesNotContain(new ScoreGameKey(3, ScoreGameKind.Overtime), bo5Keys);
        Assert.Contains(new ScoreGameKey(4, ScoreGameKind.Normal), bo5Keys);
        Assert.Contains(new ScoreGameKey(5, ScoreGameKind.Overtime), bo5Keys);
    }

    [Fact]
    public void CompleteCellTemplateUsesBoSpecificScoreGameKeys()
    {
        var bo3Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(
            majorGameGap: 197,
            halfGameGap: 90,
            isBo3Mode: true);
        var bo5Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(
            majorGameGap: 197,
            halfGameGap: 90,
            isBo3Mode: false);

        Assert.Equal(8, bo3Cells.Count);
        Assert.Contains(bo3Cells, cell => cell is
        {
            Id: "Game3OvertimeFirstHalf",
            GameNumber: 3,
            GameKind: ScoreGameKind.Overtime,
            HalfKind: ScoreHalfKind.FirstHalf,
            Visibility: FrontedControlVisibility.Visible,
            X: 591
        });
        Assert.DoesNotContain(bo3Cells, cell => cell.Id == "Game4FirstHalf");

        Assert.Equal(12, bo5Cells.Count);
        Assert.Contains(bo5Cells, cell => cell is
        {
            Id: "Game4FirstHalf",
            GameNumber: 4,
            GameKind: ScoreGameKind.Normal,
            HalfKind: ScoreHalfKind.FirstHalf,
            Visibility: FrontedControlVisibility.Visible,
            X: 591
        });
        Assert.DoesNotContain(bo5Cells, cell => cell.Id == "Game3OvertimeFirstHalf");
    }

    [Fact]
    public void AutoArrangeBo3PlacesGame3OvertimeAsFourthGroup()
    {
        var row = new GlobalScoreRowControlConfig
        {
            MajorGameGap = 200,
            HalfGameGap = 50,
            Cells = GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate(isBo3Mode: true)
        };

        GlobalScoreRowCellLayoutHelper.AutoArrangeBySpacing(row, isBo3Mode: true);

        Assert.Contains(row.Cells, cell => cell is { Id: "Game3OvertimeFirstHalf", X: 600 });
        Assert.Contains(row.Cells, cell => cell is { Id: "Game3OvertimeSecondHalf", X: 650 });
    }

    private static GlobalScoreCellConfig Cell(
        int gameNumber,
        ScoreGameKind gameKind,
        ScoreHalfKind halfKind) =>
        new()
        {
            Id = $"Game{gameNumber}{halfKind}",
            GameNumber = gameNumber,
            GameKind = gameKind,
            HalfKind = halfKind,
            X = 12,
            Y = 3,
            Width = 75,
            Height = 32
        };
}
