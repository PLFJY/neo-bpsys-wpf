using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class MatchScoreServiceTest
{
    [Fact]
    public void SetCurrentHalfResultWritesToCurrentGameMatchScore()
    {
        var currentGame = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Game1FirstHalf);
        var sharedDataService = new Mock<ISharedDataService>();
        sharedDataService.Setup(service => service.CurrentGame).Returns(currentGame);
        sharedDataService.Setup(service => service.IsBo3Mode).Returns(false);

        var service = new MatchScoreService(
            sharedDataService.Object,
            NullLogger<MatchScoreService>.Instance);

        service.SetCurrentHalfResult(GameResult.Escape3);

        var half = currentGame.MatchScore.GetHalf(GameProgress.Game1FirstHalf)!;
        Assert.Equal(GameResult.Escape3, half.Result);
        Assert.Equal(TeamType.HomeTeam, half.SurTeamTypeWhenRecorded);
        Assert.Equal(TeamType.AwayTeam, half.HunTeamTypeWhenRecorded);
        Assert.Equal(3, half.HomeMinorScore);
        Assert.Equal(1, half.AwayMinorScore);
    }
}
