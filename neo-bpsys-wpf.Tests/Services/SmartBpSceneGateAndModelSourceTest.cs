extern alias smartbp;

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Moq;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;
using SmartBpSceneGateService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpSceneGateService;
using SmartBpRecognitionScene = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionScene;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpPhaseRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpPhaseRecognitionResult;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpCroppedFrame = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpCroppedFrame;
using SmartBpOcrRecognitionRequest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionRequest;
using SmartBpSnapshotDeltaRequest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaRequest;
using SmartBpRecognitionRegion = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionRegion;
using SmartBpLifecycleCategory = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpLifecycleCategory;
using SmartBpAutoRecognitionCoordinator = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutoRecognitionCoordinator;
using SmartBpCandidateOperationBuilder = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCandidateOperationBuilder;
using SmartBpPlayerIdentityMatcher = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpPlayerIdentityMatcher;
using SmartBpHistoricalFrameReviewService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpHistoricalFrameReviewService;
using ISmartBpRegionSnapshotRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRegionSnapshotRecognitionService;
using ISmartBpSnapshotDeltaRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpSnapshotDeltaRecognitionService;
using ISmartBpSnapshotRecognitionPlanner = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpSnapshotRecognitionPlanner;
using ISmartBpFrameRingBuffer = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpFrameRingBuffer;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using ISmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpCharacterResolver;
using ISmartBpSceneGateService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpSceneGateService;
using ISmartBpDebugLog = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpDebugLog;
using ISmartBpOcrBpRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpOcrBpRecognitionService;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpSceneGateAndModelSourceTest
{
    [Theory]
    [InlineData(SmartBpLifecycleCategory.CharacterBpActive, "banned_sur,banned_hun,picked_sur,picked_hun")]
    [InlineData(SmartBpLifecycleCategory.SurvivorTalentAdjust, "banned_sur,banned_hun,picked_sur,picked_hun")]
    [InlineData(SmartBpLifecycleCategory.HunterTalentAdjust, "banned_sur,banned_hun,picked_sur,picked_hun")]
    [InlineData(SmartBpLifecycleCategory.TransitionToAreaSelection, "")]
    [InlineData(SmartBpLifecycleCategory.Unknown, "")]
    public void LifecycleFieldFilterAllowsOnlyCategorySafeFields(SmartBpLifecycleCategory category, string expected)
    {
        var request = new SmartBpSnapshotDeltaRequest(
        [
            (SmartBpRecognitionRegion.RightTop, "banned_sur"),
            (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
            (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
            (SmartBpRecognitionRegion.RightBottom, "picked_hun")
        ], []);

        var result = SmartBpAutoRecognitionCoordinator.FilterAutomaticRequestByLifecycle(request, category);

        Assert.Equal(expected, string.Join(',', result.RequestedFields));
    }

    [Theory]
    [InlineData("求生者选择角色中", "banned_sur,banned_hun,picked_sur,picked_hun")]
    [InlineData("求生者选择天赋中", "banned_sur,banned_hun,picked_sur,picked_hun")]
    [InlineData("选择监管者", "banned_sur,banned_hun,picked_sur,picked_hun")]
    public void AutomaticFieldFilterKeepsOnlyFieldsAllowedByAuthoritativePhase(string phase, string expectedField)
    {
        var request = new SmartBpSnapshotDeltaRequest(
        [
            (SmartBpRecognitionRegion.RightTop, "banned_sur"),
            (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
            (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
            (SmartBpRecognitionRegion.RightBottom, "picked_hun")
        ], []);

        var filtered = SmartBpAutoRecognitionCoordinator.FilterAutomaticRequestByPhase(request, phase);

        Assert.Equal(expectedField.Split(','), filtered.RequestedFields);
    }

    [Theory]
    [InlineData("求生者选择区域中")]
    [InlineData("天赋已锁定")]
    [InlineData("未知")]
    public void AutomaticFieldFilterBlocksAllContentOutsideCharacterPhases(string phase)
    {
        var request = new SmartBpSnapshotDeltaRequest(
            [(SmartBpRecognitionRegion.RightBottom, "picked_hun")], []);

        var filtered = SmartBpAutoRecognitionCoordinator.FilterAutomaticRequestByPhase(request, phase);

        Assert.Empty(filtered.RequestedFields);
    }

    [Fact]
    public async Task StopAsync_CancelsInFlightRecognitionImmediately()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var planner = new Mock<ISmartBpSnapshotRecognitionPlanner>();
            planner.Setup(service => service.BuildRequest(It.IsAny<GameGuidanceRuntimeSnapshot>()))
                .Returns(new SmartBpSnapshotDeltaRequest([], []));
            var recognitionSettings = new Mock<ISmartBpRecognitionSettingsService>();
            recognitionSettings.SetupGet(service => service.Settings).Returns(new SmartBpRecognitionSettings
            {
            });
            var guidance = new Mock<IGameGuidanceService>();
            guidance.Setup(service => service.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 0, null, [], null, []));
            var shared = new Mock<ISharedDataService>();
            var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
            ocr.Setup(service => service.RecognizeAsync(
                    It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async (BitmapSource _, SmartBpOcrRecognitionRequest _, CancellationToken token) =>
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return new smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionResult
                    {
                        Phase = new SmartBpPhaseRecognitionResult { Phase = "未知" }
                    };
                });
            var coordinator = new SmartBpAutoRecognitionCoordinator(
                planner.Object, Mock.Of<ISmartBpFrameRingBuffer>(), recognitionSettings.Object, guidance.Object,
                Mock.Of<ICharacterSelectionService>(),
                new SmartBpCandidateOperationBuilder(Mock.Of<ISmartBpCharacterResolver>(), shared.Object, new SmartBpPlayerIdentityMatcher(shared.Object)),
                new SmartBpSceneGateService(), ocr.Object,
                new SmartBpHistoricalFrameReviewService(Mock.Of<ISmartBpFrameRingBuffer>(), ocr.Object,
                    Mock.Of<ICharacterSelectionService>(), recognitionSettings.Object),
                Mock.Of<smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpReconciliationService>());
            await coordinator.StartAsync();
            var frame = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            var tick = coordinator.RunOneTickAsync(frame);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var watch = Stopwatch.StartNew();
            await coordinator.StopAsync();
            watch.Stop();

            Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(500));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tick);
        });
    }

    [Fact]
    public async Task OcrAutomatic_LocalPostBpOverridesStalePhaseBeforeContentOcr()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var frame = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            var planner = new Mock<ISmartBpSnapshotRecognitionPlanner>();
            planner.Setup(service => service.BuildRequest(It.IsAny<GameGuidanceRuntimeSnapshot>()))
                .Returns(new SmartBpSnapshotDeltaRequest(
                    [(SmartBpRecognitionRegion.RightTop, "banned_sur")],
                    ["test requested banned_sur"]));
            var recognitionSettings = new Mock<ISmartBpRecognitionSettingsService>();
            recognitionSettings.SetupGet(service => service.Settings).Returns(new SmartBpRecognitionSettings
            {
                EnableAutoApplyRecognition = true,
                EnableAutoGuidanceSync = true
            });
            var guidance = new Mock<IGameGuidanceService>();
            guidance.Setup(service => service.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 0, null, [], null, []));
            var ocr = new Mock<ISmartBpOcrBpRecognitionService>(MockBehavior.Strict);
            ocr.Setup(service => service.RecognizeAsync(
                    It.IsAny<BitmapSource>(),
                    It.Is<SmartBpOcrRecognitionRequest>(request =>
                        !request.IncludePhase &&
                        request.ContentRegions.SequenceEqual(new[]
                        {
                            SmartBpRecognitionRegion.TopCenterStatus,
                            SmartBpRecognitionRegion.TopLeftStatus
                        })),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionResult
                {
                    Phase = new SmartBpPhaseRecognitionResult { Phase = "求生者选择区域中" },
                    Diagnostics = ["local status detected"]
                });
            var coordinator = new SmartBpAutoRecognitionCoordinator(
                planner.Object, Mock.Of<ISmartBpFrameRingBuffer>(), recognitionSettings.Object, guidance.Object,
                Mock.Of<ICharacterSelectionService>(),
                new SmartBpCandidateOperationBuilder(Mock.Of<ISmartBpCharacterResolver>(), Mock.Of<ISharedDataService>(), new SmartBpPlayerIdentityMatcher(Mock.Of<ISharedDataService>())),
                new SmartBpSceneGateService(), ocr.Object,
                new SmartBpHistoricalFrameReviewService(Mock.Of<ISmartBpFrameRingBuffer>(), ocr.Object,
                    Mock.Of<ICharacterSelectionService>(), recognitionSettings.Object),
                Mock.Of<smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpReconciliationService>());
            await coordinator.StartAsync();

            var result = await coordinator.RunOneTickAsync(frame);

            Assert.True(result.SceneGate?.ShouldPauseAutomaticRecognition);
            Assert.Equal("求生者选择区域中", result.PhaseResult?.Phase);
            Assert.Empty(result.Operations);
            Assert.Contains(result.CandidateMessages, message => message.Contains(
                "TopLeftStatus hard confirmation", StringComparison.Ordinal));
            ocr.Verify(service => service.RecognizeAsync(
                It.IsAny<BitmapSource>(),
                It.IsAny<SmartBpOcrRecognitionRequest>(),
                It.IsAny<CancellationToken>()), Times.Once);
            var latched = await coordinator.RunOneTickAsync(frame);
            Assert.Contains(latched.CandidateMessages, message => message.Contains("Post-BP latch already set", StringComparison.Ordinal));
            ocr.Verify(service => service.RecognizeAsync(
                It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()), Times.Once);

            await coordinator.CompleteAsync();
            await coordinator.StartAsync();
            var restarted = await coordinator.RunOneTickAsync(frame);
            Assert.DoesNotContain(restarted.CandidateMessages, message => message.Contains("Post-BP latch already set", StringComparison.Ordinal));
            ocr.Verify(service => service.RecognizeAsync(
                It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        });
    }

    [Fact]
    public async Task FullSnapshotRequestsAllBusinessRegionsWithPhaseIndependentParseContract()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var frame = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            var settings = new Mock<ISmartBpRecognitionSettingsService>();
            settings.SetupGet(service => service.Settings).Returns(new SmartBpRecognitionSettings());
            var guidance = new Mock<IGameGuidanceService>();
            guidance.Setup(service => service.GetRuntimeSnapshot()).Returns(
                new GameGuidanceRuntimeSnapshot(true, 0, GameAction.BanSur, [2], null,
                    [new GameGuidanceStepSnapshot(0, GameAction.BanSur, [2], null)]));
            SmartBpOcrRecognitionRequest? contentRequest = null;
            var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
            ocr.Setup(service => service.RecognizeAsync(
                    It.IsAny<BitmapSource>(), It.Is<SmartBpOcrRecognitionRequest>(request => request.IncludePhase), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionResult
                {
                    Phase = new SmartBpPhaseRecognitionResult { Phase = "屏蔽求生者" }
                });
            ocr.Setup(service => service.RecognizeAsync(
                    It.IsAny<BitmapSource>(), It.Is<SmartBpOcrRecognitionRequest>(request => !request.IncludePhase), It.IsAny<CancellationToken>()))
                .Callback<BitmapSource, SmartBpOcrRecognitionRequest, CancellationToken>((_, request, _) => contentRequest = request)
                .ReturnsAsync(new smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionResult
                {
                    Phase = new SmartBpPhaseRecognitionResult { Phase = "未知" },
                    BusinessState = new smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult()
                });
            var shared = Mock.Of<ISharedDataService>();
            var coordinator = new SmartBpAutoRecognitionCoordinator(
                Mock.Of<ISmartBpSnapshotRecognitionPlanner>(), Mock.Of<ISmartBpFrameRingBuffer>(), settings.Object, guidance.Object,
                Mock.Of<ICharacterSelectionService>(),
                new SmartBpCandidateOperationBuilder(Mock.Of<ISmartBpCharacterResolver>(), shared, new SmartBpPlayerIdentityMatcher(shared)),
                new SmartBpSceneGateService(), ocr.Object,
                new SmartBpHistoricalFrameReviewService(Mock.Of<ISmartBpFrameRingBuffer>(), ocr.Object,
                    Mock.Of<ICharacterSelectionService>(), settings.Object),
                Mock.Of<smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpReconciliationService>());

            var result = await coordinator.RecognizeFullBpSnapshotAsync(frame, isDryRun: true);

            Assert.NotNull(result.BusinessState);
            Assert.NotNull(contentRequest);
            Assert.Equal(
                [
                    SmartBpRecognitionRegion.RightTop,
                    SmartBpRecognitionRegion.LeftTop,
                    SmartBpRecognitionRegion.LeftBottom,
                    SmartBpRecognitionRegion.RightBottom
                ],
                contentRequest!.ContentRegions);
            Assert.True(contentRequest.ParseContext?.IsGlobalSnapshot);
            Assert.Null(contentRequest.ParseContext?.CurrentGuidanceAction);
            Assert.False(contentRequest.ParseContext?.SurvivorPickLocked);
        });
    }

    [Theory]
    [InlineData("规则设置", SmartBpRecognitionScene.RulesDialog, false, false)]
    [InlineData("查看禁选顺序", SmartBpRecognitionScene.BanPickOrderDialog, false, false)]
    [InlineData("屏蔽求生者", SmartBpRecognitionScene.CharacterBp, true, false)]
    [InlineData("求生者天赋特质调整", SmartBpRecognitionScene.SurvivorTalent, true, false)]
    [InlineData("监管者天赋特质调整", SmartBpRecognitionScene.HunterTalent, true, false)]
    [InlineData("天赋已锁定", SmartBpRecognitionScene.TalentLocked, false, false)]
    [InlineData("即将进入区域选择", SmartBpRecognitionScene.OutOfBp, false, true)]
    [InlineData("区域选择", SmartBpRecognitionScene.OutOfBp, false, true)]
    [InlineData("求生者选择区域中", SmartBpRecognitionScene.AreaSelectionSurvivor, false, true)]
    [InlineData("监管者选择区域中", SmartBpRecognitionScene.AreaSelectionHunter, false, true)]
    [InlineData("等待游戏开始", SmartBpRecognitionScene.WaitingGameStart, false, true)]
    [InlineData("加载中", SmartBpRecognitionScene.Loading, false, true)]
    [InlineData("对局中", SmartBpRecognitionScene.InGame, false, true)]
    [InlineData("密码机尚未破译", SmartBpRecognitionScene.InGame, false, true)]
    public void Classify_GatesCharacterOperationsByScene(
        string evidence,
        SmartBpRecognitionScene expectedScene,
        bool expectedCharacterOperations,
        bool expectedPause)
    {
        var result = new SmartBpSceneGateService().Classify(
            new() { Phase = evidence },
            new() { Phase = evidence },
            new Dictionary<string, string> { ["phase"] = evidence },
            new GameGuidanceRuntimeSnapshot(true, 0, null, [], null, []));

        Assert.Equal(expectedScene, result.Scene);
        Assert.Equal(expectedCharacterOperations, result.IsCharacterOperationAllowed);
        Assert.Equal(expectedPause, result.ShouldPauseAutomaticRecognition);
        if (expectedPause)
        {
            Assert.False(result.IsBpRecognitionAllowed);
            Assert.Contains("queued operations drain", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

}
