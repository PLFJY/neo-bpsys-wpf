#nullable enable

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

/// <summary>
/// 测试主窗口对局进度同步行为。
/// </summary>
public sealed class MainWindowViewModelGameProgressSyncTest
{
    /// <summary>
    /// 验证外部服务修改对局进度时只同步 MainWindow 选择值，不发布用户选择教程信号。
    /// </summary>
    [Fact]
    public async Task GameProgressChanged_SyncsSelectedProgressWithoutPublishingSelectionSignal()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var game = CreateGame(GameProgress.Free);
            var shared = CreateSharedDataService(game);
            var tutorialSignal = new Mock<ITutorialSignalService>();
            var viewModel = CreateViewModel(shared.Object, tutorialSignal.Object);

            game.GameProgress = GameProgress.Game1FirstHalf;
            shared.Raise(service => service.GameProgressChanged += null, EventArgs.Empty);

            Assert.Equal(GameProgress.Game1FirstHalf, viewModel.SelectedGameProgress);
            tutorialSignal.Verify(
                service => service.Publish(TutorialSignalIds.GameProgressSelectedBo1FirstHalf, It.IsAny<object?>()),
                Times.Never);

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 验证用户选择第一局上半时仍会写回共享对局并发布教程信号。
    /// </summary>
    [Fact]
    public async Task SelectedGameProgress_SetByUser_WritesGameAndPublishesSelectionSignal()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var game = CreateGame(GameProgress.Free);
            var shared = CreateSharedDataService(game);
            var tutorialSignal = new Mock<ITutorialSignalService>();
            var viewModel = CreateViewModel(shared.Object, tutorialSignal.Object);

            viewModel.SelectedGameProgress = GameProgress.Game1FirstHalf;

            Assert.Equal(GameProgress.Game1FirstHalf, game.GameProgress);
            tutorialSignal.Verify(
                service => service.Publish(TutorialSignalIds.GameProgressSelectedBo1FirstHalf, GameProgress.Game1FirstHalf),
                Times.Once);

            return Task.CompletedTask;
        });
    }

    private static MainWindowViewModel CreateViewModel(
        ISharedDataService sharedDataService,
        ITutorialSignalService tutorialSignalService) =>
        new(
            sharedDataService,
            new Mock<IGameGuidanceService>().Object,
            new Mock<IFilePickerService>().Object,
            tutorialSignalService,
            CreateSmartBpAutoRecognitionGlobalControl().Object,
            NullLogger<MainWindowViewModel>.Instance);

    private static Mock<ISharedDataService> CreateSharedDataService(Game game)
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(service => service.CurrentGame).Returns(game);
        shared.SetupProperty(service => service.IsBo3Mode, false);
        shared.SetupGet(service => service.RemainingSeconds).Returns("VS");
        return shared;
    }

    private static Mock<ISmartBpAutoRecognitionGlobalControl> CreateSmartBpAutoRecognitionGlobalControl()
    {
        var control = new Mock<ISmartBpAutoRecognitionGlobalControl>();
        control.SetupGet(service => service.IsRunning).Returns(false);
        control.Setup(service => service.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return control;
    }

    private static Game CreateGame(GameProgress progress) =>
        new(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            progress);
}
