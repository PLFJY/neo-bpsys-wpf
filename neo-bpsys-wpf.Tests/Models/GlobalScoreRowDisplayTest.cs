using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
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
}
