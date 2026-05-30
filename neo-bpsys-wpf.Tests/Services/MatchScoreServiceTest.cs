using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Linq;
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

    [Fact]
    public void ScorePageCommandUpdatesCurrentGameMatchScoreCurrentHalf()
    {
        var (currentGame, sharedDataService, frontedWindowService, service) =
            CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, frontedWindowService.Object, service);

        viewModel.Escape3Command.Execute(null);

        var half = service.GetHalf(GameProgress.Game1FirstHalf)!;
        Assert.Equal(GameResult.Escape3, half.Result);
        Assert.Equal(3, currentGame.MatchScore.HomeTotalMinorScore);
        Assert.Equal(1, currentGame.MatchScore.AwayTotalMinorScore);
    }

    [Fact]
    public void ScorePageClearCommandSetsCurrentHalfResultToNull()
    {
        var (_, sharedDataService, frontedWindowService, service) =
            CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, frontedWindowService.Object, service);
        viewModel.Escape4Command.Execute(null);

        viewModel.ClearCurrentHalfScoreCommand.Execute(null);

        var half = service.GetHalf(GameProgress.Game1FirstHalf)!;
        Assert.Null(half.Result);
        Assert.Null(half.SurTeamTypeWhenRecorded);
        Assert.Null(half.HunTeamTypeWhenRecorded);
    }

    [Fact]
    public void MajorScoreIsDerivedAfterBothHalvesAreSet()
    {
        var (currentGame, sharedDataService, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);
        currentGame.Swap();
        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        service.SetCurrentHalfResult(GameResult.Out4);

        Assert.Equal(1, sharedDataService.Object.HomeTeam.Score.Win);
        Assert.Equal(0, sharedDataService.Object.AwayTeam.Score.Win);
        Assert.Equal(1, currentGame.MatchScore.HomeMajorWin);
        Assert.Equal(0, currentGame.MatchScore.AwayMajorWin);
    }

    [Fact]
    public void NullHalfPreventsScoreGameFromParticipatingInMajorCalculation()
    {
        var (currentGame, _, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape4);
        currentGame.GameProgress = GameProgress.Game1SecondHalf;
        service.ClearCurrentHalfResult();

        Assert.Equal(0, currentGame.MatchScore.HomeMajorWin);
        Assert.Equal(0, currentGame.MatchScore.AwayMajorWin);
        Assert.Equal(5, currentGame.MatchScore.HomeTotalMinorScore);
        Assert.Equal(0, currentGame.MatchScore.AwayTotalMinorScore);
    }

    [Fact]
    public void FreeGameProgressScorePageCommandDoesNotCrashOrWriteScore()
    {
        var (currentGame, sharedDataService, frontedWindowService, service) =
            CreateScorePageTestServices(GameProgress.Free);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, frontedWindowService.Object, service);

        viewModel.Out4Command.Execute(null);

        Assert.All(currentGame.MatchScore.Games.SelectMany(game => new[] { game.FirstHalf, game.SecondHalf }),
            half => Assert.Null(half.Result));
        Assert.Equal(0, currentGame.MatchScore.HomeTotalMinorScore);
        Assert.Equal(0, currentGame.MatchScore.AwayTotalMinorScore);
    }

    [Fact]
    public void LegacyTeamScoreMirrorUpdatesFromMatchScoreState()
    {
        var (currentGame, sharedDataService, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);

        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        Assert.Equal(3, sharedDataService.Object.HomeTeam.Score.GameScores);
        Assert.Equal(1, sharedDataService.Object.AwayTeam.Score.GameScores);
    }

    [Fact]
    public void FirstHalfPreScoreDisplaysZeroForBothCamps()
    {
        var (currentGame, _, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);

        service.RefreshCurrentProgress();

        Assert.Equal("0", currentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText);
        Assert.Equal("0", currentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText);
    }

    [Fact]
    public void SecondHalfPreScoreUsesFirstHalfMinorScoreForSameMapping()
    {
        var (currentGame, _, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);

        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        Assert.Equal("3", currentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText);
        Assert.Equal("1", currentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText);
    }

    [Fact]
    public void SecondHalfPreScoreMapsFirstHalfMinorScoreToCurrentCampsAfterSwap()
    {
        var (currentGame, _, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);

        currentGame.Swap();
        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        Assert.Equal("1", currentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText);
        Assert.Equal("3", currentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText);
    }

    [Fact]
    public void CurrentCampMajorTextUpdatesAfterBothHalvesAreRecorded()
    {
        var (currentGame, _, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);
        currentGame.Swap();
        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        service.SetCurrentHalfResult(GameResult.Out4);

        Assert.Equal("W0  D0", currentGame.MatchScore.CurrentSurTeamMajorText);
        Assert.Equal("W1  D0", currentGame.MatchScore.CurrentHunTeamMajorText);
    }

    [Fact]
    public void ScoreWindowTotalMinorScoreValuesComeFromMatchScoreState()
    {
        var (currentGame, sharedDataService, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);
        var settingsHostService = new Mock<ISettingsHostService>();
        settingsHostService.Setup(service => service.Settings).Returns(new Settings());
        var viewModel = new ScoreWindowViewModel(sharedDataService.Object, settingsHostService.Object);

        viewModel.Receive(new CommunityToolkit.Mvvm.Messaging.Messages.PropertyChangedMessage<int>(
            this,
            nameof(ScoreWindowViewModel.TotalMainGameScore),
            0,
            999));

        Assert.Equal(currentGame.MatchScore.HomeTotalMinorScore, viewModel.TotalMainGameScore);
        Assert.Equal(currentGame.MatchScore.AwayTotalMinorScore, viewModel.TotalAwayGameScore);
    }

    private static (
        Game CurrentGame,
        Mock<ISharedDataService> SharedDataService,
        Mock<IFrontedWindowService> FrontedWindowService,
        MatchScoreService MatchScoreService) CreateScorePageTestServices(GameProgress progress)
    {
        var homeTeam = new Team(Camp.Sur, TeamType.HomeTeam);
        var awayTeam = new Team(Camp.Hun, TeamType.AwayTeam);
        var currentGame = new Game(homeTeam, awayTeam, progress);
        var sharedDataService = new Mock<ISharedDataService>();
        sharedDataService.Setup(service => service.HomeTeam).Returns(homeTeam);
        sharedDataService.Setup(service => service.AwayTeam).Returns(awayTeam);
        sharedDataService.Setup(service => service.CurrentGame).Returns(currentGame);
        sharedDataService.Setup(service => service.IsBo3Mode).Returns(false);

        var frontedWindowService = new Mock<IFrontedWindowService>();
        var matchScoreService = new MatchScoreService(
            sharedDataService.Object,
            NullLogger<MatchScoreService>.Instance);

        return (currentGame, sharedDataService, frontedWindowService, matchScoreService);
    }
}
