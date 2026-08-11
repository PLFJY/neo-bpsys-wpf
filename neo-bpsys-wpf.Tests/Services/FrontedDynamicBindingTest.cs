using Moq;
using neo_bpsys_wpf.WebRenderer.Services;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class FrontedDynamicBindingTest
{
    [Theory]
    [InlineData("Foo[CurrentGame.Index].Bar")]
    [InlineData("Foo[0].Bar")]
    [InlineData("Foo['abc'].Bar")]
    [InlineData("Foo[CurrentGame.Index].Items[Other.Index].Name")]
    public void ParserAcceptsSupportedPaths(string path)
    {
        Assert.True(FrontedBindingPathParser.TryParse(path, out _, out var error), error?.Message);
    }

    [Theory]
    [InlineData("Foo[")]
    [InlineData("Foo[]")]
    [InlineData("Foo[A + 1]")]
    [InlineData("Foo[A ? B : C]")]
    public void ParserRejectsExpressions(string path)
    {
        Assert.False(FrontedBindingPathParser.TryParse(path, out _, out _));
    }

    [Fact]
    public void ValidatorReportsTheInvalidDynamicIndexSegment()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Picture",
            Config = new ImageFrontedControlConfig
            {
                BindingPath = "CurrentGame.SurPlayerList[CurrentGame.NotAnIndex].Member.Name"
            }
        };
        var document = new FrontedCanvasDesignDocument
        {
            WindowTypeName = "TestWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig(),
            Controls = new ObservableCollection<FrontedControlDesignItem> { item }
        };

        var message = Assert.Single(
            new FrontedLayoutValidator().Validate(document),
            candidate => candidate.Code == "DynamicBindingPathInvalid");

        Assert.Contains("NotAnIndex", message.Message);
    }

    [Fact]
    public void DynamicIndexerReactsToIndexItemCollectionAndIntermediateChanges()
    {
        WpfTestThread.Run(() =>
        {
            var firstGame = CreateGame(GameProgress.Game1FirstHalf, "One", "Two");
            var secondGame = CreateGame(GameProgress.Game1SecondHalf, "Three", "Four");
            var sharedData = new Mock<ISharedDataService>();
            var currentGame = firstGame;
            sharedData.SetupGet(service => service.CurrentGame).Returns(() => currentGame);

            var text = new TextBlock();
            BindingOperations.SetBinding(text, TextBlock.TextProperty, FrontedBindingFactory.Create(
                "CurrentGame.SurPlayerList[CurrentGame.GameProgress].Member.Name",
                sharedData.Object));

            Assert.Equal("One", text.Text);

            firstGame.GameProgress = GameProgress.Game1SecondHalf;
            Assert.Equal("Two", text.Text);

            firstGame.SurPlayerList[1].Member.Name = "Updated";
            Assert.Equal("Updated", text.Text);

            firstGame.SurPlayersData[1] = new Player(new Member(Camp.Sur) { Name = "Replacement" });
            Assert.Equal("Replacement", text.Text);

            firstGame.SurPlayersData.Clear();
            Assert.Equal(string.Empty, text.Text);

            currentGame = secondGame;
            sharedData.Raise(service => service.PropertyChanged += null,
                new System.ComponentModel.PropertyChangedEventArgs(nameof(ISharedDataService.CurrentGame)));
            Assert.Equal("Four", text.Text);

            currentGame = null!;
            sharedData.Raise(service => service.PropertyChanged += null,
                new System.ComponentModel.PropertyChangedEventArgs(nameof(ISharedDataService.CurrentGame)));
            Assert.Equal(string.Empty, text.Text);

            currentGame = secondGame;
            secondGame.GameProgress = GameProgress.Game5OvertimeSecondHalf;
            sharedData.Raise(service => service.PropertyChanged += null,
                new System.ComponentModel.PropertyChangedEventArgs(nameof(ISharedDataService.CurrentGame)));
            Assert.Equal(string.Empty, text.Text);
        });
    }

    [Fact]
    public void ScoreHalfByProgressResolvesBoAliasReactively()
    {
        WpfTestThread.Run(() =>
        {
            var game = CreateGame(GameProgress.Game4FirstHalf, "One", "Two");
            var state = game.MatchScore;
            SetHalf(state.GetHalf(GameProgress.Game4FirstHalf, isBo3Mode: false)!, GameResult.Escape4);
            SetHalf(state.GetHalf(GameProgress.Game3OvertimeFirstHalf, isBo3Mode: true)!, GameResult.Out4);
            state.Recalculate(isBo3Mode: false);

            var sharedData = new Mock<ISharedDataService>();
            sharedData.SetupGet(service => service.CurrentGame).Returns(game);
            var text = new TextBlock();
            BindingOperations.SetBinding(text, TextBlock.TextProperty, FrontedBindingFactory.Create(
                "CurrentGame.MatchScore.HalfByProgress[CurrentGame.GameProgress].SurMinorScore",
                sharedData.Object));

            Assert.Equal("5", text.Text);
            state.Recalculate(isBo3Mode: true);
            Assert.Equal("0", text.Text);
        });
    }

    [Fact]
    public void WebRendererResolverUsesTheSameDynamicIndexSyntax()
    {
        var game = CreateGame(GameProgress.Game4FirstHalf, "One", "Two");
        SetHalf(game.MatchScore.GetHalf(GameProgress.Game4FirstHalf, isBo3Mode: false)!, GameResult.Escape4);
        SetHalf(game.MatchScore.GetHalf(GameProgress.Game3OvertimeFirstHalf, isBo3Mode: true)!, GameResult.Out4);
        var sharedData = new Mock<ISharedDataService>();
        sharedData.SetupGet(service => service.CurrentGame).Returns(game);

        game.MatchScore.Recalculate(isBo3Mode: false);
        var bo5 = WebRendererBindingPathResolver.Resolve(
            sharedData.Object,
            "CurrentGame.MatchScore.HalfByProgress[CurrentGame.GameProgress].SurMinorScore");
        game.MatchScore.Recalculate(isBo3Mode: true);
        var bo3 = WebRendererBindingPathResolver.Resolve(
            sharedData.Object,
            "CurrentGame.MatchScore.HalfByProgress[CurrentGame.GameProgress].SurMinorScore");

        Assert.Equal(5, bo5.Value);
        Assert.Equal(0, bo3.Value);
    }

    private static Game CreateGame(GameProgress progress, string firstName, string secondName)
    {
        var game = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            progress);
        game.SurPlayerList[0].Member.Name = firstName;
        game.SurPlayerList[1].Member.Name = secondName;
        return game;
    }

    private static void SetHalf(ScoreHalf half, GameResult result)
    {
        half.Result = result;
        half.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        half.HunTeamTypeWhenRecorded = TeamType.AwayTeam;
    }
}
