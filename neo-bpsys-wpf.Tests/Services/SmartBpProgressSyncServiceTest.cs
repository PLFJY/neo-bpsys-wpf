extern alias smartbp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using Moq;
using Xunit;
using ISmartBpDetectedOperationApplier = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpDetectedOperationApplier;
using ISmartBpGameStateSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpGameStateSyncService;
using ISmartBpProgressSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpProgressSyncService;
using ISmartBpRecognitionLedger = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionLedger;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using ISmartBpWorkflowBackfillService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpWorkflowBackfillService;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpGameStateSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpGameStateSyncService;
using SmartBpOperationApplyResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOperationApplyResult;
using SmartBpProgressInferenceOptions = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpProgressInferenceOptions;
using SmartBpProgressSyncResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpProgressSyncResult;
using SmartBpProgressInferenceService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpProgressInferenceService;
using SmartBpProgressSyncMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpProgressSyncMode;
using SmartBpProgressSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpProgressSyncService;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpRecognizedCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedCharacterSlot;
using SmartBpRecognizedPlayerCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedPlayerCharacterSlot;
using SmartBpWorkflowBackfillPlan = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpWorkflowBackfillPlan;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpProgressSyncServiceTest
{
    [Fact]
    public void InferenceDistinguishesSameActionPickSurSteps()
    {
        var service = new SmartBpProgressInferenceService();
        var result = service.Infer(
            State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1], pickedSur: [0, 1]),
            Snapshot(2, Workflow()));

        Assert.True(result.IsConfident, result.Reason);
        Assert.Equal(3, result.TargetStepIndex);
        Assert.Equal(GameAction.PickSur, result.TargetAction);
        Assert.Equal([2], result.TargetIndexes);
    }

    [Fact]
    public void InferenceSelectsFirstPickSurWhenSurvivorPicksAreEmpty()
    {
        var service = new SmartBpProgressInferenceService();
        var result = service.Infer(
            State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1]),
            Snapshot(2, Workflow()));

        Assert.True(result.IsConfident, result.Reason);
        Assert.Equal(2, result.TargetStepIndex);
        Assert.Equal([0, 1], result.TargetIndexes);
    }

    [Fact]
    public void PickSurPhaseDoesNotFallBackToNearbyBanSurStep()
    {
        var service = new SmartBpProgressInferenceService();
        var result = service.Infer(
            State("选择求生者"),
            Snapshot(0, Workflow()));

        Assert.False(result.IsConfident);
        Assert.Equal(GameAction.PickSur, result.TargetAction);
    }

    [Fact]
    public void PickSurPhaseWithCompletedPreviousBansSelectsPickSurFromEarlyCurrentStep()
    {
        var service = new SmartBpProgressInferenceService();
        var result = service.Infer(
            State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1]),
            Snapshot(0, Workflow()));

        Assert.True(result.IsConfident, result.Reason);
        Assert.Equal(2, result.TargetStepIndex);
        Assert.Equal(GameAction.PickSur, result.TargetAction);
    }

    [Fact]
    public void InferenceSelectsDistributeCharaWhenAllSurvivorsPicked()
    {
        var workflow = new[]
        {
            new GameGuidanceStepSnapshot(0, GameAction.PickSur, [0, 1], null),
            new GameGuidanceStepSnapshot(1, GameAction.PickSur, [2], null),
            new GameGuidanceStepSnapshot(2, GameAction.PickSur, [3], null),
            new GameGuidanceStepSnapshot(3, GameAction.DistributeChara, [0, 1, 2, 3], null)
        };
        var service = new SmartBpProgressInferenceService();
        var result = service.Infer(
            State("求生者选择角色中", pickedSur: [0, 1, 2, 3]),
            Snapshot(2, workflow));

        Assert.True(result.IsConfident, result.Reason);
        Assert.Equal(3, result.TargetStepIndex);
        Assert.Equal(GameAction.DistributeChara, result.TargetAction);
    }

    [Fact]
    public void InferenceSelectsHunterTalentWhenHunterPicked()
    {
        var workflow = new[]
        {
            new GameGuidanceStepSnapshot(0, GameAction.PickHun, [0], null),
            new GameGuidanceStepSnapshot(1, GameAction.PickHunTalent, [], null)
        };
        var service = new SmartBpProgressInferenceService();
        var result = service.Infer(
            State("监管者选择天赋中", pickedHun: true),
            Snapshot(0, workflow));

        Assert.True(result.IsConfident, result.Reason);
        Assert.Equal(1, result.TargetStepIndex);
        Assert.Equal(GameAction.PickHunTalent, result.TargetAction);
    }

    [Fact]
    public void UnknownSlotsDoNotForceFuturePickSurStep()
    {
        var observed = State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1], pickedSur: [0, 1]);
        observed.PickedSur[2].CharacterName = "unknown";
        observed.PickedSur[3].CharacterName = "unknown";
        var service = new SmartBpProgressInferenceService();
        var result = service.Infer(observed, Snapshot(2, Workflow()));

        Assert.False(result.IsConfident);
        Assert.NotEqual(4, result.TargetStepIndex);
    }

    [Fact]
    public void AlignmentReportsMisalignedWhenCurrentStepDiffers()
    {
        var sync = CreateSync(Snapshot(2, Workflow()));
        var alignment = sync.CheckAlignment(
            State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1], pickedSur: [0, 1]),
            Snapshot(2, Workflow()),
            new SmartBpProgressInferenceOptions(true, null, .70, .10));

        Assert.True(alignment.IsMisaligned);
        Assert.Equal(3, alignment.Inference.TargetStepIndex);
    }

    [Fact]
    public async Task ManualForceSyncCanMoveBackward()
    {
        var guidance = new FakeGuidance(Snapshot(3, Workflow()));
        var sync = CreateSync(guidance);

        var result = await sync.ForceSyncAsync(
            State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1]),
            SmartBpProgressSyncMode.Manual);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Moved);
        Assert.Equal(2, guidance.CurrentStepIndex);
    }

    [Fact]
    public async Task ManualForceSyncUsesPhaseWhenSlotEvidenceIsSparse()
    {
        var guidance = new FakeGuidance(Snapshot(0, Workflow()));
        var sync = CreateSync(guidance);

        var result = await sync.ForceSyncAsync(
            State("选择求生者"),
            SmartBpProgressSyncMode.Manual);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Moved);
        Assert.Equal(2, guidance.CurrentStepIndex);
        Assert.Equal(GameAction.PickSur, result.TargetAction);
    }

    [Fact]
    public async Task ManualForceSyncStartsGuidanceThenMovesToInferredStep()
    {
        var guidance = new FakeGuidance(Snapshot(-1, Workflow(), isStarted: false));
        var sync = CreateSync(guidance);

        var result = await sync.ForceSyncAsync(
            State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1], pickedSur: [0, 1]),
            SmartBpProgressSyncMode.Manual);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Moved);
        Assert.True(guidance.IsGuidanceStarted);
        Assert.Equal(3, guidance.CurrentStepIndex);
    }

    [Fact]
    public async Task AutomaticForceSyncDoesNotMoveBackward()
    {
        var guidance = new FakeGuidance(Snapshot(3, Workflow()));
        var sync = CreateSync(guidance);

        var result = await sync.ForceSyncAsync(
            State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1]),
            SmartBpProgressSyncMode.AutomaticDiagnostic);

        Assert.False(result.Succeeded);
        Assert.Equal(3, guidance.CurrentStepIndex);
    }

    [Fact]
    public async Task GameStateSyncResetsLedgerThenAppliesBackfillAfterProgressSync()
    {
        var observed = State("选择求生者", bannedSur: [0, 1], bannedHun: [0, 1]);
        var progress = new Mock<ISmartBpProgressSyncService>();
        progress.Setup(service => service.ForceSyncAsync(observed, SmartBpProgressSyncMode.Manual, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartBpProgressSyncResult(true, true, 0, 2, GameAction.PickSur, [0, 1], "moved", ["progress"]));
        var guidance = new Mock<IGameGuidanceService>();
        guidance.Setup(service => service.GetRuntimeSnapshot()).Returns(Snapshot(2, Workflow()));
        var ledger = new Mock<ISmartBpRecognitionLedger>();
        var backfill = new Mock<ISmartBpWorkflowBackfillService>();
        backfill.Setup(service => service.BuildPlan(observed, It.IsAny<GameGuidanceRuntimeSnapshot>()))
            .Returns(new SmartBpWorkflowBackfillPlan([], ["plan"]));
        var applier = new Mock<ISmartBpDetectedOperationApplier>();
        applier.Setup(service => service.ApplyAsync(It.IsAny<IReadOnlyList<smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartBpOperationApplyResult(2, 1, ["applied"]));
        var service = new SmartBpGameStateSyncService(progress.Object, guidance.Object, ledger.Object, backfill.Object, applier.Object);

        var result = await service.ForceSyncAsync(observed);

        Assert.Equal(2, result.ApplyResult?.AppliedCount);
        ledger.Verify(item => item.ResetForCurrentGame(), Times.Once);
        backfill.Verify(item => item.BuildPlan(observed, It.IsAny<GameGuidanceRuntimeSnapshot>()), Times.Once);
        applier.Verify(item => item.ApplyAsync(It.IsAny<IReadOnlyList<smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperation>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GameStateSyncDoesNotApplyStateWhenProgressSyncFails()
    {
        var observed = State("未知");
        var progress = new Mock<ISmartBpProgressSyncService>();
        progress.Setup(service => service.ForceSyncAsync(observed, SmartBpProgressSyncMode.Manual, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartBpProgressSyncResult(false, false, 0, null, null, [], "ambiguous", []));
        var ledger = new Mock<ISmartBpRecognitionLedger>();
        var backfill = new Mock<ISmartBpWorkflowBackfillService>();
        var applier = new Mock<ISmartBpDetectedOperationApplier>();
        var service = new SmartBpGameStateSyncService(progress.Object, Mock.Of<IGameGuidanceService>(), ledger.Object, backfill.Object, applier.Object);

        var result = await service.ForceSyncAsync(observed);

        Assert.Null(result.ApplyResult);
        ledger.Verify(item => item.ResetForCurrentGame(), Times.Never);
        backfill.Verify(item => item.BuildPlan(It.IsAny<SmartBpBusinessStateRecognitionResult>(), It.IsAny<GameGuidanceRuntimeSnapshot>()), Times.Never);
        applier.Verify(item => item.ApplyAsync(It.IsAny<IReadOnlyList<smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperation>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SmartBpProgressSyncService CreateSync(GameGuidanceRuntimeSnapshot snapshot) =>
        CreateSync(new FakeGuidance(snapshot));

    private static SmartBpProgressSyncService CreateSync(FakeGuidance guidance) =>
        new(guidance, new FakeSettings(), new SmartBpProgressInferenceService());

    private static GameGuidanceStepSnapshot[] Workflow() =>
    [
        new(0, GameAction.BanSur, [0, 1], null),
        new(1, GameAction.BanHun, [0, 1], null),
        new(2, GameAction.PickSur, [0, 1], null),
        new(3, GameAction.PickSur, [2], null),
        new(4, GameAction.PickSur, [3], null)
    ];

    private static GameGuidanceRuntimeSnapshot Snapshot(
        int currentStep,
        IReadOnlyList<GameGuidanceStepSnapshot> workflow,
        bool isStarted = true)
    {
        var current = currentStep >= 0 && currentStep < workflow.Count ? workflow[currentStep] : null;
        return new(isStarted, currentStep, current?.Action, current?.Indexes ?? [], current?.Time, workflow);
    }

    private static SmartBpBusinessStateRecognitionResult State(
        string phase,
        IReadOnlyCollection<int>? bannedSur = null,
        IReadOnlyCollection<int>? bannedHun = null,
        IReadOnlyCollection<int>? pickedSur = null,
        bool pickedHun = false) =>
        new()
        {
            Phase = phase,
            BannedSur = CharacterSlots(4, bannedSur),
            BannedHun = CharacterSlots(2, bannedHun),
            PickedSur = PlayerSlots(4, pickedSur),
            PickedHun = new SmartBpRecognizedPlayerCharacterSlot { Index = 0, CharacterName = pickedHun ? "厂长" : "未选择" }
        };

    private static List<SmartBpRecognizedCharacterSlot> CharacterSlots(int count, IReadOnlyCollection<int>? selected)
    {
        var set = selected ?? [];
        var slots = new List<SmartBpRecognizedCharacterSlot>();
        for (var i = 0; i < count; i++)
            slots.Add(new SmartBpRecognizedCharacterSlot { Index = i, CharacterName = set.Contains(i) ? $"角色{i}" : "未选择" });
        return slots;
    }

    private static List<SmartBpRecognizedPlayerCharacterSlot> PlayerSlots(int count, IReadOnlyCollection<int>? selected)
    {
        var set = selected ?? [];
        var slots = new List<SmartBpRecognizedPlayerCharacterSlot>();
        for (var i = 0; i < count; i++)
            slots.Add(new SmartBpRecognizedPlayerCharacterSlot { Index = i, CharacterName = set.Contains(i) ? $"求生者{i}" : "未选择" });
        return slots;
    }

    private sealed class FakeSettings : ISmartBpRecognitionSettingsService
    {
        public SmartBpRecognitionSettings Settings { get; } = new()
        {
            EnableAutoGuidancePageNavigation = true,
            GuidanceSyncLookAheadSteps = 4,
            SmartBpProgressInferenceMinimumScore = .82,
            SmartBpProgressInferenceMinimumScoreMargin = .15
        };

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGuidance(GameGuidanceRuntimeSnapshot snapshot) : IGameGuidanceService
    {
        private readonly IReadOnlyList<GameGuidanceStepSnapshot> _workflow = snapshot.Workflow;

        public int CurrentStepIndex { get; private set; } = snapshot.CurrentStepIndex;

        public bool IsGuidanceStarted { get; set; } = snapshot.IsStarted;

        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStateChanged;
        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStarted;
        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStopped;
        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceCancelled;
        public event EventHandler<GameGuidanceStepChangedEventArgs>? GuidanceStepChanged;
        public event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightChanged;
        public event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightCleared;

        public Task<string?> StartGuidance(bool isNavigatePageEnable = true)
        {
            IsGuidanceStarted = true;
            if (CurrentStepIndex < 0 && _workflow.Count > 0)
                CurrentStepIndex = 0;
            return Task.FromResult<string?>("Started step 0");
        }

        public Task<string?> NextStepAsync(bool isNavigatePageEnable = true) => Task.FromResult<string?>(null);

        public Task<string?> PrevStepAsync(bool isNavigatePageEnable = true) => Task.FromResult<string?>(null);

        public GameGuidanceRuntimeSnapshot GetRuntimeSnapshot()
        {
            var current = _workflow.FirstOrDefault(step => step.StepIndex == CurrentStepIndex);
            return new(IsGuidanceStarted, CurrentStepIndex, current?.Action, current?.Indexes ?? [], current?.Time, _workflow);
        }

        public Task<string?> MoveToStepAsync(int stepIndex, bool isNavigatePageEnable = true)
        {
            CurrentStepIndex = stepIndex;
            return Task.FromResult<string?>(null);
        }

        public void StopGuidance() => IsGuidanceStarted = false;

        public void CompleteGuidance(string reason = "SmartBpCharacterBpEnded") => IsGuidanceStarted = false;
    }
}
