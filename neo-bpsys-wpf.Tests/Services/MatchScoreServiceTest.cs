using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;

using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        var (currentGame, sharedDataService, service) =
            CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, service);

        viewModel.Escape3Command.Execute(null);

        var half = service.GetHalf(GameProgress.Game1FirstHalf)!;
        Assert.Equal(GameResult.Escape3, half.Result);
        Assert.Equal(3, currentGame.MatchScore.HomeTotalMinorScore);
        Assert.Equal(1, currentGame.MatchScore.AwayTotalMinorScore);
    }

    [Fact]
    public void ScorePageClearCommandSetsCurrentHalfResultToNull()
    {
        var (_, sharedDataService, service) =
            CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, service);
        viewModel.Escape4Command.Execute(null);

        viewModel.ClearCurrentHalfScoreCommand.Execute(null);

        var half = service.GetHalf(GameProgress.Game1FirstHalf)!;
        Assert.Null(half.Result);
        Assert.Null(half.SurTeamTypeWhenRecorded);
        Assert.Null(half.HunTeamTypeWhenRecorded);
    }

    [Fact]
    public void ScorePageSelectedResultFollowsCurrentGameProgress()
    {
        var (currentGame, sharedDataService, service) =
            CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, service);

        viewModel.SelectedCurrentHalfResult = GameResult.Escape3;
        Assert.Equal(GameResult.Escape3, service.GetHalf(GameProgress.Game1FirstHalf)!.Result);
        Assert.Equal(GameResult.Escape3, viewModel.SelectedCurrentHalfResult);

        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        Assert.True(viewModel.IsScoreControlEnabled);
        Assert.Null(viewModel.SelectedCurrentHalfResult);

        viewModel.SelectedCurrentHalfResult = GameResult.Out4;
        Assert.Equal(GameResult.Out4, service.GetHalf(GameProgress.Game1SecondHalf)!.Result);

        currentGame.GameProgress = GameProgress.Game1FirstHalf;

        Assert.Equal(GameResult.Escape3, viewModel.SelectedCurrentHalfResult);
    }

    [Fact]
    public void ScorePageSelectedResultDoesNotWriteWhenProgressIsFree()
    {
        var (currentGame, sharedDataService, service) =
            CreateScorePageTestServices(GameProgress.Free);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, service);

        viewModel.SelectedCurrentHalfResult = GameResult.Out4;

        Assert.False(viewModel.IsScoreControlEnabled);
        Assert.Null(viewModel.SelectedCurrentHalfResult);
        Assert.All(currentGame.MatchScore.Games.SelectMany(game => new[] { game.FirstHalf, game.SecondHalf }),
            half => Assert.Null(half.Result));
    }

    [Fact]
    public void MajorScoreIsDerivedAfterBothHalvesAreSet()
    {
        var (currentGame, sharedDataService, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);
        currentGame.Swap();
        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        service.SetCurrentHalfResult(GameResult.Out4);

        Assert.Equal(1, currentGame.MatchScore.HomeMajorWin);
        Assert.Equal(0, currentGame.MatchScore.AwayMajorWin);
    }

    [Fact]
    public void NullHalfPreventsScoreGameFromParticipatingInMajorCalculation()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
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
        var (currentGame, sharedDataService, service) =
            CreateScorePageTestServices(GameProgress.Free);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, service);

        viewModel.Out4Command.Execute(null);

        Assert.All(currentGame.MatchScore.Games.SelectMany(game => new[] { game.FirstHalf, game.SecondHalf }),
            half => Assert.Null(half.Result));
        Assert.Equal(0, currentGame.MatchScore.HomeTotalMinorScore);
        Assert.Equal(0, currentGame.MatchScore.AwayTotalMinorScore);
    }

    [Fact]
    public void NoRuntimeLegacyMirror()
    {
        var (currentGame, sharedDataService, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        sharedDataService.Object.HomeTeam.Score.GameScores = 42;
        sharedDataService.Object.AwayTeam.Score.GameScores = 24;

        service.SetCurrentHalfResult(GameResult.Escape3);

        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        Assert.Null(typeof(IMatchScoreService).GetMethod("SyncLegacyTeamScoreMirror"));
        Assert.Null(typeof(MatchScoreService).GetMethod(
            "SyncLegacyTeamScoreMirror",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal(42, sharedDataService.Object.HomeTeam.Score.GameScores);
        Assert.Equal(24, sharedDataService.Object.AwayTeam.Score.GameScores);
    }

    [Fact]
    public void FirstHalfCurrentScoreDisplaysZeroBeforeResult()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);

        service.RefreshCurrentProgress();

        Assert.Equal("0", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("0", currentGame.MatchScore.CurrentHunTeamMinorScoreText);
    }

    [Fact]
    public void FirstHalfCurrentScoreDisplaysLiveMinorScoreAfterResult()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);

        service.SetCurrentHalfResult(GameResult.Escape3);

        Assert.Equal("3", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("1", currentGame.MatchScore.CurrentHunTeamMinorScoreText);
    }

    [Fact]
    public void SecondHalfAccumulatesWithFirstHalf()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);

        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        // 第二半未记录 → 继承第一半累计：第一半 Escape3 → Sur=3, Hun=1
        Assert.Equal("3", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("1", currentGame.MatchScore.CurrentHunTeamMinorScoreText);

        // 第二半记录 Out4 → 累计：第一半 3:1 + 第二半 0:5 = Sur 3+0=3, Hun 1+5=6
        service.SetCurrentHalfResult(GameResult.Out4);
        Assert.Equal("3", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("6", currentGame.MatchScore.CurrentHunTeamMinorScoreText);
    }

    [Fact]
    public void SecondHalfAccumulatesTieWithFirstHalfEscape4()
    {
        // 用户场景：第一半 5:0、第二半录入平局 2:2 → 应显示 7:2
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape4);

        Assert.Equal("5", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("0", currentGame.MatchScore.CurrentHunTeamMinorScoreText);

        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        // 第二半未记录 → 继承第一半累计 5:0
        Assert.Equal("5", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("0", currentGame.MatchScore.CurrentHunTeamMinorScoreText);

        // 第二半记录 Tie → 累计 5+2=7, 0+2=2
        service.SetCurrentHalfResult(GameResult.Tie);
        Assert.Equal("7", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("2", currentGame.MatchScore.CurrentHunTeamMinorScoreText);
    }

    [Fact]
    public void SecondHalfAccumulatesWithFirstHalfAfterSwap()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);

        currentGame.Swap();
        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        // 换边后 Sur=Away, Hun=Home。第一半 Escape3 → Home=3, Away=1
        // 第二半未记录 → 累计只含第一半，按当前阵营映射：Sur=Away→1, Hun=Home→3
        Assert.Equal("1", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("3", currentGame.MatchScore.CurrentHunTeamMinorScoreText);

        // 第二半记录 Out4（当前 Sur=Away → Home=Hun=5, Away=Sur=0）
        // 累计：Home=3+5=8, Away=1+0=1 → Sur=Away→1, Hun=Home→8
        service.SetCurrentHalfResult(GameResult.Out4);
        Assert.Equal("1", currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal("8", currentGame.MatchScore.CurrentHunTeamMinorScoreText);
    }

    [Fact]
    public void CurrentHalfAndGameScoreExposeDistinctTeamMappedLevels()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape4);

        currentGame.Swap();
        currentGame.GameProgress = GameProgress.Game1SecondHalf;
        service.SetCurrentHalfResult(GameResult.Out3);

        var scoreGame = currentGame.MatchScore.GetGame(GameProgress.Game1SecondHalf)!;
        var scoreHalf = scoreGame.SecondHalf;

        Assert.Same(scoreGame, currentGame.MatchScore.CurrentGameScore);
        Assert.Same(scoreHalf, currentGame.MatchScore.CurrentHalf);
        Assert.Equal("1", currentGame.MatchScore.CurrentSurTeamMinorHalfScoreText);
        Assert.Equal("3", currentGame.MatchScore.CurrentHunTeamMinorHalfScoreText);
        Assert.Equal("1", currentGame.MatchScore.CurrentSurTeamMinorGameScoreText);
        Assert.Equal("8", currentGame.MatchScore.CurrentHunTeamMinorGameScoreText);
        Assert.Equal(currentGame.MatchScore.CurrentSurTeamMinorGameScoreText,
            currentGame.MatchScore.CurrentSurTeamMinorScoreText);
        Assert.Equal(currentGame.MatchScore.CurrentHunTeamMinorGameScoreText,
            currentGame.MatchScore.CurrentHunTeamMinorScoreText);
    }

    [Fact]
    public void UnrecordedCurrentHalfShowsDashWithoutChangingGameScore()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape4);

        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        Assert.Same(currentGame.MatchScore.Games.Single(game =>
                game.Key == new ScoreGameKey(1, ScoreGameKind.Normal)).SecondHalf,
            currentGame.MatchScore.CurrentHalf);
        Assert.Equal("-", currentGame.MatchScore.CurrentSurTeamMinorHalfScoreText);
        Assert.Equal("-", currentGame.MatchScore.CurrentHunTeamMinorHalfScoreText);
        Assert.Equal("5", currentGame.MatchScore.CurrentSurTeamMinorGameScoreText);
        Assert.Equal("0", currentGame.MatchScore.CurrentHunTeamMinorGameScoreText);
    }

    [Fact]
    public void CurrentScoreObjectsNotifyWhenProgressAndHalfChange()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.RefreshCurrentProgress();
        var changedProperties = new HashSet<string>();
        currentGame.MatchScore.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
                changedProperties.Add(args.PropertyName);
        };

        currentGame.GameProgress = GameProgress.Game1SecondHalf;
        service.SetCurrentHalfResult(GameResult.Tie);

        Assert.Contains(nameof(MatchScoreState.CurrentGameScore), changedProperties);
        Assert.Contains(nameof(MatchScoreState.CurrentHalf), changedProperties);
        Assert.Contains(nameof(MatchScoreState.CurrentSurTeamMinorHalfScoreText), changedProperties);
        Assert.Contains(nameof(MatchScoreState.CurrentHunTeamMinorHalfScoreText), changedProperties);
        Assert.Contains(nameof(MatchScoreState.CurrentSurTeamMinorGameScoreText), changedProperties);
        Assert.Contains(nameof(MatchScoreState.CurrentHunTeamMinorGameScoreText), changedProperties);
        Assert.Contains(nameof(MatchScoreState.CurrentSurTeamMinorScoreText), changedProperties);
        Assert.Contains(nameof(MatchScoreState.CurrentHunTeamMinorScoreText), changedProperties);
    }

    [Fact]
    public void CurrentCampMajorTextUpdatesAfterBothHalvesAreRecorded()
    {
        var (currentGame, _, service) = CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        service.SetCurrentHalfResult(GameResult.Escape3);
        currentGame.Swap();
        currentGame.GameProgress = GameProgress.Game1SecondHalf;

        service.SetCurrentHalfResult(GameResult.Out4);

        Assert.Equal("W0  D0", currentGame.MatchScore.CurrentSurTeamMajorText);
        Assert.Equal("W1  D0", currentGame.MatchScore.CurrentHunTeamMajorText);
    }

    [Fact]
    public void OnIsBo3ModeChangedCallsRecalculate()
    {
        var (currentGame, sharedDataService, service) =
            CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        var isBo3Mode = false;
        sharedDataService.Setup(service => service.IsBo3Mode).Returns(() => isBo3Mode);
        FillGame(currentGame.MatchScore, new ScoreGameKey(3, ScoreGameKind.Overtime), GameResult.Escape3);
        FillGame(currentGame.MatchScore, new ScoreGameKey(4, ScoreGameKind.Normal), GameResult.Escape4);
        service.Recalculate();
        Assert.Equal(10, currentGame.MatchScore.HomeTotalMinorScore);

        isBo3Mode = true;
        sharedDataService.Raise(service => service.IsBo3ModeChanged += null, EventArgs.Empty);

        Assert.Equal(6, currentGame.MatchScore.HomeTotalMinorScore);
        Assert.Equal(2, currentGame.MatchScore.AwayTotalMinorScore);
    }

    [Fact]
    public void CurrentHalfResolutionStillUsesBoMode()
    {
        var (bo3Game, _, bo3Service) =
            CreateScorePageTestServices(GameProgress.Game3OvertimeFirstHalf, isBo3Mode: true);
        var (bo5Game, _, bo5Service) =
            CreateScorePageTestServices(GameProgress.Game4FirstHalf, isBo3Mode: false);

        bo3Service.RefreshCurrentProgress();
        bo5Service.RefreshCurrentProgress();

        Assert.Equal(
            new ScoreGameKey(3, ScoreGameKind.Overtime),
            bo3Service.CurrentGameScore?.Key);
        Assert.Equal(bo3Service.CurrentGameScore, bo3Game.MatchScore.CurrentGameScore);
        Assert.Same(bo3Game.MatchScore.CurrentGameScore?.FirstHalf, bo3Game.MatchScore.CurrentHalf);
        Assert.Equal(
            new ScoreGameKey(4, ScoreGameKind.Normal),
            bo5Service.CurrentGameScore?.Key);
        Assert.Equal(bo5Service.CurrentGameScore, bo5Game.MatchScore.CurrentGameScore);
        Assert.Same(bo5Game.MatchScore.CurrentGameScore?.FirstHalf, bo5Game.MatchScore.CurrentHalf);
    }

    [Fact]
    public void ScorePageResetClearsPreviewRows()
    {
        var (currentGame, sharedDataService, service) =
            CreateScorePageTestServices(GameProgress.Game1FirstHalf);
        var viewModel = new ScorePageViewModel(sharedDataService.Object, service);
        viewModel.Escape4Command.Execute(null);
        currentGame.GameProgress = GameProgress.Game1SecondHalf;
        viewModel.Out4Command.Execute(null);

        viewModel.ResetCommand.Execute(null);

        Assert.All(viewModel.ScorePreviewRows, row =>
        {
            Assert.False(row.HasResult);
            Assert.Equal("-", row.ResultText);
            Assert.Equal("-", row.HomeMinorScoreText);
            Assert.Equal("-", row.AwayMinorScoreText);
        });
    }

    private static (
        Game CurrentGame,
        Mock<ISharedDataService> SharedDataService,
        MatchScoreService MatchScoreService) CreateScorePageTestServices(GameProgress progress, bool isBo3Mode = false)
    {
        var homeTeam = new Team(Camp.Sur, TeamType.HomeTeam);
        var awayTeam = new Team(Camp.Hun, TeamType.AwayTeam);
        var currentGame = new Game(homeTeam, awayTeam, progress);
        var sharedDataService = new Mock<ISharedDataService>();
        sharedDataService.Setup(service => service.HomeTeam).Returns(homeTeam);
        sharedDataService.Setup(service => service.AwayTeam).Returns(awayTeam);
        sharedDataService.Setup(service => service.CurrentGame).Returns(currentGame);
        sharedDataService.Setup(service => service.IsBo3Mode).Returns(isBo3Mode);

        var matchScoreService = new MatchScoreService(
            sharedDataService.Object,
            NullLogger<MatchScoreService>.Instance);

        return (currentGame, sharedDataService, matchScoreService);
    }

    private static void FillGame(MatchScoreState state, ScoreGameKey key, GameResult result)
    {
        var game = state.Games.Single(scoreGame => scoreGame.Key == key);
        SetHalf(game.FirstHalf, result);
        SetHalf(game.SecondHalf, result);
    }

    private static void SetHalf(ScoreHalf half, GameResult result)
    {
        half.Result = result;
        half.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        half.HunTeamTypeWhenRecorded = TeamType.AwayTeam;
    }
}
