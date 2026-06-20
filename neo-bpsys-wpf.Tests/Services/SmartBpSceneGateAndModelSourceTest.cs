extern alias smartbp;

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Moq;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;
using QwenModelAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.QwenModelAssetManager;
using SmartBpParallelDownload = smartbp::neo_bpsys_wpf.Services.SmartBpParallelDownload;
using SmartBpSceneGateService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpSceneGateService;
using QwenModelProfile = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.QwenModelProfile;
using QwenModelSourceType = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.QwenModelSourceType;
using SmartBpRecognitionScene = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionScene;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpPhaseRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpPhaseRecognitionResult;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpSnapshotDeltaRequest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaRequest;
using SmartBpRecognitionLedgerSnapshot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionLedgerSnapshot;
using SmartBpAutoRecognitionCoordinator = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutoRecognitionCoordinator;
using SmartBpCandidateOperationBuilder = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCandidateOperationBuilder;
using ISmartBpRegionSnapshotRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRegionSnapshotRecognitionService;
using ISmartBpSnapshotDeltaRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpSnapshotDeltaRecognitionService;
using ISmartBpSnapshotRecognitionPlanner = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpSnapshotRecognitionPlanner;
using ISmartBpRecognitionStateStore = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionStateStore;
using ISmartBpRecognitionLedger = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionLedger;
using ISmartBpFrameRingBuffer = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpFrameRingBuffer;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using ISmartBpGuidanceSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpGuidanceSyncService;
using ISmartBpWorkflowBackfillService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpWorkflowBackfillService;
using ISmartBpDetectedOperationApplier = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpDetectedOperationApplier;
using ISmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpCharacterResolver;
using ISmartBpSceneGateService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpSceneGateService;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpSceneGateAndModelSourceTest
{
    [Fact]
    public void ManagedAssetDownloads_UseUpdaterGradeParallelConfiguration()
    {
        var configuration = SmartBpParallelDownload.CreateConfiguration(
            new Uri("https://huggingface.co/owner/repo/model.gguf"));

        Assert.True(configuration.ParallelDownload);
        Assert.Equal(8, configuration.ChunkCount);
        Assert.Equal(6, configuration.ParallelCount);
        Assert.Equal(5, configuration.MaxTryAgainOnFailure);
        Assert.True(configuration.EnableAutoResumeDownload);
        Assert.True(configuration.CheckDiskSizeBeforeDownload);
    }

    [Fact]
    public async Task StopAsync_CancelsInFlightRecognitionImmediately()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var delta = new Mock<ISmartBpSnapshotDeltaRecognitionService>();
            delta.Setup(service => service.RecognizeDeltaAsync(
                    It.IsAny<BitmapSource>(), It.IsAny<SmartBpSnapshotDeltaRequest>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(async (BitmapSource _, SmartBpSnapshotDeltaRequest _, long _, CancellationToken token) =>
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return default;
                });
            var planner = new Mock<ISmartBpSnapshotRecognitionPlanner>();
            planner.Setup(service => service.BuildRequest(It.IsAny<GameGuidanceRuntimeSnapshot>(), It.IsAny<SmartBpBusinessStateRecognitionResult>(), It.IsAny<SmartBpRecognitionLedgerSnapshot>()))
                .Returns(new SmartBpSnapshotDeltaRequest([], []));
            var state = new Mock<ISmartBpRecognitionStateStore>();
            state.SetupGet(service => service.Snapshot).Returns(new SmartBpBusinessStateRecognitionResult());
            var ledger = new Mock<ISmartBpRecognitionLedger>();
            ledger.Setup(service => service.GetSnapshot()).Returns(new SmartBpRecognitionLedgerSnapshot([]));
            var recognitionSettings = new Mock<ISmartBpRecognitionSettingsService>();
            recognitionSettings.SetupGet(service => service.Settings).Returns(new SmartBpRecognitionSettings { UseMultiImageSnapshotRequest = true });
            var guidance = new Mock<IGameGuidanceService>();
            guidance.Setup(service => service.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 0, null, [], null, []));
            var shared = new Mock<ISharedDataService>();
            var coordinator = new SmartBpAutoRecognitionCoordinator(
                Mock.Of<ISmartBpRegionSnapshotRecognitionService>(), delta.Object, planner.Object, state.Object,
                ledger.Object, Mock.Of<ISmartBpFrameRingBuffer>(), recognitionSettings.Object, shared.Object,
                Mock.Of<ISmartBpGuidanceSyncService>(), guidance.Object, Mock.Of<ISmartBpWorkflowBackfillService>(),
                new SmartBpCandidateOperationBuilder(Mock.Of<ISmartBpCharacterResolver>(), shared.Object),
                Mock.Of<ISmartBpDetectedOperationApplier>(), Mock.Of<ISmartBpSceneGateService>());
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

    [Theory]
    [InlineData("规则设置", SmartBpRecognitionScene.RulesDialog, false, false)]
    [InlineData("查看禁选顺序", SmartBpRecognitionScene.BanPickOrderDialog, false, false)]
    [InlineData("屏蔽求生者", SmartBpRecognitionScene.CharacterBp, true, false)]
    [InlineData("求生者天赋特质调整", SmartBpRecognitionScene.SurvivorTalent, false, false)]
    [InlineData("监管者天赋特质调整", SmartBpRecognitionScene.HunterTalent, false, false)]
    [InlineData("天赋已锁定", SmartBpRecognitionScene.TalentLocked, false, true)]
    [InlineData("求生者选择区域中", SmartBpRecognitionScene.AreaSelectionSurvivor, false, true)]
    [InlineData("监管者选择区域中", SmartBpRecognitionScene.AreaSelectionHunter, false, true)]
    [InlineData("等待游戏开始", SmartBpRecognitionScene.WaitingGameStart, false, true)]
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
    }

    [Fact]
    public void ResolveDownloadUrl_BuildsOfficialHuggingFaceUrl()
    {
        var profile = HuggingFaceProfile();
        var result = QwenModelAssetManager.ResolveDownloadUrl(
            profile, profile.ModelFileName, false, new(), CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal("https://huggingface.co/owner/repo/resolve/main/model.gguf", result);
    }

    [Fact]
    public void ResolveDownloadUrl_UsesChineseMirrorAndEndpointOverride()
    {
        var profile = HuggingFaceProfile();
        var mirror = QwenModelAssetManager.ResolveDownloadUrl(
            profile, profile.ModelFileName, false, new(), CultureInfo.GetCultureInfo("zh-CN"));
        var overridden = QwenModelAssetManager.ResolveDownloadUrl(
            profile, profile.ModelFileName, false,
            new SmartBpRecognitionSettings { HuggingFaceEndpointOverride = "https://models.example.test/" },
            CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal("https://hf-mirror.com/owner/repo/resolve/main/model.gguf", mirror);
        Assert.Equal("https://models.example.test/owner/repo/resolve/main/model.gguf", overridden);
    }

    [Fact]
    public void ResolveDownloadUrl_PreservesDirectUrlProfiles()
    {
        var profile = new QwenModelProfile
        {
            Id = "direct", SourceType = QwenModelSourceType.DirectUrl,
            ModelUrl = "https://example.test/model.gguf", ModelFileName = "model.gguf"
        };
        var result = QwenModelAssetManager.ResolveDownloadUrl(
            profile, profile.ModelFileName, false, new(), CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal(profile.ModelUrl, result);
    }

    private static QwenModelProfile HuggingFaceProfile() => new()
    {
        Id = "hf", SourceType = QwenModelSourceType.HuggingFace,
        HuggingFaceRepoId = "owner/repo", HuggingFaceRevision = "main",
        ModelFileName = "model.gguf", UseHuggingFaceMirrorForChineseUi = true
    };
}
