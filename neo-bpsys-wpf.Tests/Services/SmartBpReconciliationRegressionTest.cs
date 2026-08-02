extern alias smartbp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;
using ISmartBpDetectedOperationApplier = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpDetectedOperationApplier;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using SmartBpCandidateOperationBuilder = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCandidateOperationBuilder;
using SmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCharacterResolver;
using SmartBpDetectedOperationApplier = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpDetectedOperationApplier;
using SmartBpFrameRingBuffer = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpFrameRingBuffer;
using SmartBpHistoricalFrameReviewService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpHistoricalFrameReviewService;
using SmartBpDetectedOperation = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperation;
using SmartBpDetectedOperationApplyMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperationApplyMode;
using SmartBpDetectedOperationKind = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperationKind;
using SmartBpPlayerIdentityMatcher = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpPlayerIdentityMatcher;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpRecognizedCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedCharacterSlot;
using SmartBpRecognizedPlayerCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedPlayerCharacterSlot;
using SmartBpRecognizedSlotState = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedSlotState;
using SmartBpReconciliationMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpReconciliationMode;
using SmartBpReconciliationService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpReconciliationService;
using SmartBpWorkflowPosition = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpWorkflowPosition;
using SmartBpCatchUpTriggerEvaluator = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCatchUpTriggerEvaluator;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpBufferedFrame = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBufferedFrame;
using SmartBpOcrRecognitionRequest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionRequest;
using SmartBpOcrRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionResult;
using SmartBpPhaseRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpPhaseRecognitionResult;
using ISmartBpFrameRingBuffer = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpFrameRingBuffer;
using ISmartBpOcrBpRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpOcrBpRecognitionService;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpReconciliationRegressionTest
{
    [Fact]
    public async Task ManualUiOperationsAndSmartBpOperationsUpdateSameCommitState()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf);
            await fixture.Selection.SelectSurvivorAsync(0, fixture.Characters["a"], false);
            await fixture.Selection.CommitEmptyBanAsync(Camp.Sur, 0, false);

            var state = fixture.Selection.GetCurrentBpSlotCommitState();

            Assert.Equal(BpSlotCommitState.CommittedCharacter, state.SurvivorPicks[0]);
            Assert.Equal(BpSlotCommitState.CommittedEmpty, state.SurvivorBans[0]);
        });
    }

    [Fact]
    public async Task SameCharacterInPendingHostSlotIsCommittedInsteadOfSkipped()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: 0);
            fixture.Game.SurPlayerList[0].Character = fixture.Characters["a"];
            Assert.Equal(BpSlotCommitState.Pending, fixture.Game.BpSlotCommitState.SurvivorPicks[0]);

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                State("选择求生者", pickedSur: [(0, "a")]),
                SmartBpReconciliationMode.Automatic);

            Assert.Equal(1, result.CharacterApplyResult.AppliedCount);
            Assert.Equal(BpSlotCommitState.CommittedCharacter, fixture.Game.BpSlotCommitState.SurvivorPicks[0]);
            fixture.Transition.Verify(service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Fact]
    public async Task HistoricalReviewSupplementsFastSkippedStepsBeforeGuidedCatchUp()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0, 1], null),
                new GameGuidanceStepSnapshot(2, GameAction.BanSur, [2], null),
                new GameGuidanceStepSnapshot(3, GameAction.PickSur, [2], null)
            };
            var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
            var banFirst = State("屏蔽求生者");
            SetSelectedBan(banFirst, 0, "a");
            SetSelectedBan(banFirst, 1, "b");
            var pickFirst = State("选择求生者", pickedSur: [(0, "c"), (1, "d")]);
            var banSecond = State("屏蔽求生者");
            SetSelectedBan(banSecond, 2, "c");
            var current = State("选择求生者", pickedSur: [(2, "a")]);
            var frame1 = FrozenFrame();
            var frame2 = FrozenFrame();
            var frame3 = FrozenFrame();
            var frames = new[]
            {
                new SmartBpBufferedFrame(1, frame1, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress),
                new SmartBpBufferedFrame(2, frame2, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress),
                new SmartBpBufferedFrame(3, frame3, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress)
            };
            var ring = new Mock<ISmartBpFrameRingBuffer>();
            ring.Setup(buffer => buffer.GetRecentFrames(It.IsAny<TimeSpan>())).Returns(frames);
            var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
            ocr.Setup(service => service.RecognizeAsync(
                    It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BitmapSource frame, SmartBpOcrRecognitionRequest _, CancellationToken _) =>
                    ReferenceEquals(frame, frame1) ? OcrResult(banFirst) :
                    ReferenceEquals(frame, frame2) ? OcrResult(pickFirst) :
                    OcrResult(banSecond));
            fixture.Settings.SetupGet(service => service.Settings).Returns(CreateSettings(3));
            var review = new SmartBpHistoricalFrameReviewService(
                ring.Object, ocr.Object, fixture.Selection, fixture.Settings.Object);

            var reviewed = await review.SupplementAsync(
                current, 4, fixture.Guidance.GetRuntimeSnapshot());
            var reconciled = await CreateReconciliation(fixture).ReconcileAsync(
                reviewed.State, SmartBpReconciliationMode.Automatic);

            Assert.Equal(5, reviewed.SupplementedSlotCount);
            Assert.Equal("a", reviewed.State.PickedSur[2].CharacterName);
            Assert.Equal(6, reconciled.CharacterApplyResult.AppliedCount);
            Assert.Equal(3, fixture.Guidance.NextStepCallCount);
            Assert.Equal(0, fixture.Guidance.DirectMoveCallCount);
            Assert.Equal(3, fixture.Guidance.CurrentStepIndex);
        });
    }

    [Fact]
    public async Task HistoricalReviewNeverOverwritesCurrentOrCommittedSlots()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.PickSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [2], null)
            };
            var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
            await fixture.Selection.SelectSurvivorAsync(1, fixture.Characters["b"], false);
            var current = State("选择求生者", pickedSur: [(2, "a")]);
            var older = State("选择求生者", pickedSur: [(0, "c"), (1, "c"), (2, "b")]);
            var newer = State("选择求生者", pickedSur: [(0, "d"), (1, "c"), (2, "b")]);
            var frame1 = FrozenFrame();
            var frame2 = FrozenFrame();
            var frames = new[]
            {
                new SmartBpBufferedFrame(1, frame1, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress),
                new SmartBpBufferedFrame(2, frame2, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress)
            };
            var ring = new Mock<ISmartBpFrameRingBuffer>();
            ring.Setup(buffer => buffer.GetRecentFrames(It.IsAny<TimeSpan>())).Returns(frames);
            var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
            ocr.Setup(service => service.RecognizeAsync(
                    It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BitmapSource frame, SmartBpOcrRecognitionRequest _, CancellationToken _) =>
                    OcrResult(ReferenceEquals(frame, frame1) ? older : newer));
            var review = new SmartBpHistoricalFrameReviewService(
                ring.Object, ocr.Object, fixture.Selection, fixture.Settings.Object);

            var reviewed = await review.SupplementAsync(
                current, 3, fixture.Guidance.GetRuntimeSnapshot());

            Assert.Equal(1, reviewed.SupplementedSlotCount);
            Assert.Equal("D", reviewed.State.PickedSur[0].CharacterName);
            Assert.Equal(SmartBpRecognizedSlotState.Unknown, reviewed.State.PickedSur[1].SlotState);
            Assert.Equal("a", reviewed.State.PickedSur[2].CharacterName);
        });
    }

    [Fact]
    public void WorkflowPositionEqualityUsesActionAndNormalizedSlotIndexes()
    {
        Assert.Equal(2, new SmartBpRecognitionSettings().OcrBackfillLookBehindSteps);
        var left = new SmartBpWorkflowPosition(GameAction.PickSur, [2, 1, 2]);
        var same = new SmartBpWorkflowPosition(GameAction.PickSur, [1, 2]);
        var differentIndexes = new SmartBpWorkflowPosition(GameAction.PickSur, [2]);
        var differentAction = new SmartBpWorkflowPosition(GameAction.BanSur, [1, 2]);

        Assert.Equal(left, same);
        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(left, differentIndexes);
        Assert.NotEqual(left, differentAction);
    }

    [Fact]
    public void CatchUpTriggerSkipsAlignedWaitingSlotAndDetectsIndexMismatch()
    {
        var workflow = new[]
        {
            new GameGuidanceStepSnapshot(0, GameAction.PickSur, [0, 1], null),
            new GameGuidanceStepSnapshot(1, GameAction.PickSur, [2], null)
        };
        var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
        var waiting = SmartBpCatchUpTriggerEvaluator.Evaluate(
            fixture.Guidance.GetRuntimeSnapshot(),
            State("选择求生者"),
            fixture.Selection.GetCurrentBpSlotCommitState());
        var advanced = SmartBpCatchUpTriggerEvaluator.Evaluate(
            fixture.Guidance.GetRuntimeSnapshot(),
            State("选择求生者", pickedSur: [(2, "c")]),
            fixture.Selection.GetCurrentBpSlotCommitState());

        Assert.False(waiting.ShouldReconcile);
        Assert.False(waiting.ShouldReviewHistory);
        Assert.True(advanced.ShouldReconcile);
        Assert.True(advanced.ShouldReviewHistory);
        Assert.True(advanced.PositionMismatch);
        Assert.Equal([2], advanced.TargetPosition?.Indexes);
    }

    [Fact]
    public void CatchUpTargetDoesNotAdvancePastTheStepWhoseSlotsWereJustObserved()
    {
        var workflow = new[]
        {
            new GameGuidanceStepSnapshot(0, GameAction.PickSur, [0, 1], null),
            new GameGuidanceStepSnapshot(1, GameAction.PickSur, [2], null)
        };
        var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);

        var decision = SmartBpCatchUpTriggerEvaluator.Evaluate(
            fixture.Guidance.GetRuntimeSnapshot(),
            State("选择求生者", pickedSur: [(0, "a"), (1, "b")]),
            fixture.Selection.GetCurrentBpSlotCommitState());

        Assert.Equal(new SmartBpWorkflowPosition(GameAction.PickSur, [0, 1]), decision.TargetPosition);
        Assert.True(decision.ShouldReconcile);
        Assert.False(decision.PositionMismatch);
    }

    [Fact]
    public void PickSurWaitingSlotDoesNotAdvanceWhileInterveningBanIsStillPending()
    {
        var workflow = ConservativePickWorkflow();
        var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 1);
        fixture.Game.BpSlotCommitState.SurvivorBans[0] = BpSlotCommitState.CommittedCharacter;
        fixture.Game.BpSlotCommitState.SurvivorBans[1] = BpSlotCommitState.CommittedCharacter;
        fixture.Game.BpSlotCommitState.SurvivorPicks[0] = BpSlotCommitState.CommittedCharacter;
        fixture.Game.BpSlotCommitState.SurvivorPicks[1] = BpSlotCommitState.CommittedCharacter;

        var decision = SmartBpCatchUpTriggerEvaluator.Evaluate(
            fixture.Guidance.GetRuntimeSnapshot(),
            State("选择求生者", pickedSur: [(0, "a"), (1, "b")]),
            fixture.Selection.GetCurrentBpSlotCommitState());

        Assert.Equal(new SmartBpWorkflowPosition(GameAction.PickSur, [0, 1]), decision.TargetPosition);
        Assert.False(decision.ShouldReconcile);
        Assert.False(decision.ShouldRewind);
    }

    [Fact]
    public async Task OvershotNextPickOccurrenceWaitsInsteadOfRewinding()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = ConservativePickWorkflow();
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 3);
            fixture.Game.BpSlotCommitState.SurvivorBans[0] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorBans[1] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorPicks[0] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorPicks[1] = BpSlotCommitState.CommittedCharacter;
            var observed = State("选择求生者", pickedSur: [(0, "a"), (1, "b")]);

            var trigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                fixture.Guidance.GetRuntimeSnapshot(),
                observed,
                fixture.Selection.GetCurrentBpSlotCommitState());
            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.Equal(new SmartBpWorkflowPosition(GameAction.PickSur, [0, 1]), trigger.TargetPosition);
            Assert.Equal(2, trigger.WorkflowStepDistance);
            Assert.False(trigger.ShouldRewind);
            Assert.False(trigger.ShouldReviewHistory);
            Assert.False(result.GuidanceResult.Moved);
            Assert.Equal(3, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(0, fixture.Guidance.PrevStepCallCount);
            Assert.Equal(0, fixture.Guidance.NextStepCallCount);
        });
    }

    [Fact]
    public void CompletedInterveningBanConfirmsNextPickStepEvenBeforeItsPickAppears()
    {
        var workflow = ConservativePickWorkflow();
        var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 3);
        fixture.Game.BpSlotCommitState.SurvivorBans[0] = BpSlotCommitState.CommittedCharacter;
        fixture.Game.BpSlotCommitState.SurvivorBans[1] = BpSlotCommitState.CommittedCharacter;
        fixture.Game.BpSlotCommitState.SurvivorPicks[0] = BpSlotCommitState.CommittedCharacter;
        fixture.Game.BpSlotCommitState.SurvivorPicks[1] = BpSlotCommitState.CommittedCharacter;
        fixture.Game.BpSlotCommitState.SurvivorBans[2] = BpSlotCommitState.CommittedCharacter;

        var decision = SmartBpCatchUpTriggerEvaluator.Evaluate(
            fixture.Guidance.GetRuntimeSnapshot(),
            State("选择求生者", pickedSur: [(0, "a"), (1, "b")]),
            fixture.Selection.GetCurrentBpSlotCommitState());

        Assert.Equal(new SmartBpWorkflowPosition(GameAction.PickSur, [2]), decision.TargetPosition);
        Assert.False(decision.ShouldReconcile);
        Assert.False(decision.ShouldRewind);
    }

    [Fact]
    public async Task SelectedNextPickProvesEmptyBanAndAdvancesForwardWithoutGuessingEarly()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = ConservativePickWorkflow();
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 1);
            fixture.Game.BpSlotCommitState.SurvivorBans[0] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorBans[1] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorPicks[0] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorPicks[1] = BpSlotCommitState.CommittedCharacter;
            var observed = State("选择求生者", pickedSur: [(0, "a"), (1, "b"), (2, "c")]);
            SetExplicitEmptyBan(observed, 2);

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.True(result.GuidanceResult.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(3, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(2, fixture.Guidance.NextStepCallCount);
            Assert.Equal(0, fixture.Guidance.PrevStepCallCount);
            Assert.Equal(BpSlotCommitState.CommittedEmpty, fixture.Game.BpSlotCommitState.SurvivorBans[2]);
            Assert.Equal("C", fixture.Game.SurPlayerList[2].Character?.Name);
        });
    }

    [Fact]
    public async Task FarAheadGuidanceRewindsOnlyAfterTargetPickIsStronglyObserved()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = ConservativePickWorkflow();
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 7);
            fixture.Game.BpSlotCommitState.SurvivorBans[0] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorBans[1] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorPicks[0] = BpSlotCommitState.CommittedCharacter;
            fixture.Game.BpSlotCommitState.SurvivorPicks[1] = BpSlotCommitState.CommittedCharacter;
            var observed = State("选择求生者", pickedSur: [(2, "c")]);
            SetExplicitEmptyBan(observed, 2);
            var trigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                fixture.Guidance.GetRuntimeSnapshot(),
                observed,
                fixture.Selection.GetCurrentBpSlotCommitState());

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.True(trigger.ShouldRewind);
            Assert.Equal(4, trigger.WorkflowStepDistance);
            Assert.Equal(3, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(5, fixture.Guidance.PrevStepCallCount);
            Assert.Equal(1, fixture.Guidance.NextStepCallCount);
            Assert.Equal(0, fixture.Guidance.DirectMoveCallCount);
            Assert.Equal(BpSlotCommitState.CommittedEmpty, fixture.Game.BpSlotCommitState.SurvivorBans[2]);
            Assert.Equal("C", fixture.Game.SurPlayerList[2].Character?.Name);
        });
    }

    [Fact]
    public async Task HistoricalReviewDoesNotRunOcrWhenActionAndIndexesAreAligned()
    {
        var workflow = new[] { new GameGuidanceStepSnapshot(0, GameAction.PickSur, [0, 1], null) };
        var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
        var ring = new Mock<ISmartBpFrameRingBuffer>();
        ring.Setup(buffer => buffer.GetRecentFrames(It.IsAny<TimeSpan>()))
            .Returns([new SmartBpBufferedFrame(1, FrozenFrame(), DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress)]);
        var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
        var review = new SmartBpHistoricalFrameReviewService(
            ring.Object, ocr.Object, fixture.Selection, fixture.Settings.Object);

        var result = await review.SupplementAsync(
            State("选择求生者"), 2, fixture.Guidance.GetRuntimeSnapshot());

        Assert.Equal(0, result.ReviewedFrameCount);
        ocr.Verify(service => service.RecognizeAsync(
            It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HistoricalReviewDefaultsToOnlyThePreviousTwoWorkflowSteps()
    {
        var workflow = new[]
        {
            new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0], null),
            new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0], null),
            new GameGuidanceStepSnapshot(2, GameAction.BanSur, [1], null),
            new GameGuidanceStepSnapshot(3, GameAction.PickSur, [1], null)
        };
        var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
        var pickFirst = State("选择求生者", pickedSur: [(0, "b")]);
        var banSecond = State("屏蔽求生者");
        SetSelectedBan(banSecond, 1, "c");
        var frame1 = FrozenFrame();
        var frame2 = FrozenFrame();
        var frame3 = FrozenFrame();
        var frames = new[]
        {
            new SmartBpBufferedFrame(1, frame1, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress),
            new SmartBpBufferedFrame(2, frame2, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress),
            new SmartBpBufferedFrame(3, frame3, DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress)
        };
        var ring = new Mock<ISmartBpFrameRingBuffer>();
        ring.Setup(buffer => buffer.GetRecentFrames(It.IsAny<TimeSpan>())).Returns(frames);
        var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
        ocr.Setup(service => service.RecognizeAsync(
                It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BitmapSource _, SmartBpOcrRecognitionRequest request, CancellationToken _) =>
                OcrResult(request.ParseContext?.CurrentGuidanceAction == GameAction.PickSur ? pickFirst : banSecond));
        var review = new SmartBpHistoricalFrameReviewService(
            ring.Object, ocr.Object, fixture.Selection, fixture.Settings.Object);

        var result = await review.SupplementAsync(
            State("选择求生者", pickedSur: [(1, "d")]), 4, fixture.Guidance.GetRuntimeSnapshot());

        Assert.Equal(2, result.ReviewedFrameCount);
        Assert.Equal(2, result.SupplementedSlotCount);
        Assert.Equal(SmartBpRecognizedSlotState.Unknown, result.State.BannedSur[0].SlotState);
        Assert.Equal("B", result.State.PickedSur[0].CharacterName);
        Assert.Equal("C", result.State.BannedSur[1].CharacterName);
        ocr.Verify(service => service.RecognizeAsync(
            It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HistoricalReviewRejectsFrameWhoseActionDoesNotMatchReviewedStep()
    {
        var workflow = new[]
        {
            new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0], null),
            new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0], null)
        };
        var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
        var historical = State("选择求生者");
        SetSelectedBan(historical, 0, "a");
        var ring = new Mock<ISmartBpFrameRingBuffer>();
        ring.Setup(buffer => buffer.GetRecentFrames(It.IsAny<TimeSpan>()))
            .Returns([new SmartBpBufferedFrame(1, FrozenFrame(), DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress)]);
        var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
        ocr.Setup(service => service.RecognizeAsync(
                It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OcrResult(historical));
        var review = new SmartBpHistoricalFrameReviewService(
            ring.Object, ocr.Object, fixture.Selection, fixture.Settings.Object);

        var result = await review.SupplementAsync(
            State("选择求生者", pickedSur: [(0, "b")]), 2, fixture.Guidance.GetRuntimeSnapshot());

        Assert.Equal(0, result.SupplementedSlotCount);
        Assert.Equal(SmartBpRecognizedSlotState.Unknown, result.State.BannedSur[0].SlotState);
        Assert.Contains(result.Diagnostics, message => message.Contains("rejected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HistoricalReviewCanSupplementACommittedEmptySlotWithoutOverwritingCharacters()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [3], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [3], null)
            };
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 0);
            await fixture.Selection.CommitEmptyBanAsync(Camp.Sur, 3, false);
            var historical = State("屏蔽求生者");
            SetSelectedBan(historical, 3, "c");
            var ring = new Mock<ISmartBpFrameRingBuffer>();
            ring.Setup(buffer => buffer.GetRecentFrames(It.IsAny<TimeSpan>()))
                .Returns([new SmartBpBufferedFrame(1, FrozenFrame(), DateTimeOffset.Now, fixture.Game.Guid, fixture.Game.GameProgress)]);
            var ocr = new Mock<ISmartBpOcrBpRecognitionService>();
            ocr.Setup(service => service.RecognizeAsync(
                    It.IsAny<BitmapSource>(), It.IsAny<SmartBpOcrRecognitionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OcrResult(historical));
            var review = new SmartBpHistoricalFrameReviewService(
                ring.Object, ocr.Object, fixture.Selection, fixture.Settings.Object);

            var reviewed = await review.SupplementAsync(
                State("选择求生者", pickedSur: [(3, "d")]),
                2,
                fixture.Guidance.GetRuntimeSnapshot());
            var reconciled = await CreateReconciliation(fixture).ReconcileAsync(
                reviewed.State, SmartBpReconciliationMode.Automatic);

            Assert.Equal(1, reviewed.ReviewedFrameCount);
            Assert.Equal(1, reviewed.SupplementedSlotCount);
            Assert.Equal(2, reconciled.CharacterApplyResult.AppliedCount);
            Assert.Equal("C", fixture.Game.CurrentSurBannedList[3]?.Name);
            Assert.Equal(BpSlotCommitState.CommittedCharacter, fixture.Game.BpSlotCommitState.SurvivorBans[3]);
            Assert.Equal(1, fixture.Guidance.CurrentStepIndex);
        });
    }

    [Fact]
    public async Task DistributeCharaPartialEvidenceFillsFirstAvailableHostSlot()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[] { new GameGuidanceStepSnapshot(0, GameAction.DistributeChara, [], null) };
            var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
            await fixture.Selection.SelectSurvivorAsync(0, fixture.Characters["a"], false);
            var observed = State("求生者选择角色中");
            observed.DistributionEvidence =
            [
                new SmartBpRecognizedPlayerCharacterSlot
                {
                    Index = 3,
                    CharacterName = "c",
                    SlotState = SmartBpRecognizedSlotState.Selected,
                    RecognitionConfidence = .99,
                    IsAutoApplySafe = true,
                    RecognitionReason = "partial safe distribution evidence"
                }
            ];

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.Equal(1, result.CharacterApplyResult.AppliedCount);
            Assert.Equal("A", fixture.Game.SurPlayerList[0].Character?.Name);
            Assert.Equal("C", fixture.Game.SurPlayerList[1].Character?.Name);
            Assert.Equal(BpSlotCommitState.CommittedCharacter, fixture.Game.BpSlotCommitState.SurvivorPicks[1]);
        });
    }

    [Fact]
    public async Task AlignedDistributeCharaEvidenceDoesNotRetriggerReconciliationWork()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[] { new GameGuidanceStepSnapshot(0, GameAction.DistributeChara, [], null) };
            var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
            var keys = new[] { "a", "b", "c", "d" };
            for (var index = 0; index < keys.Length; index++)
                await fixture.Selection.SelectSurvivorAsync(index, fixture.Characters[keys[index]], false);
            var observed = State("求生者选择角色中");
            observed.DistributionEvidence = keys.Select((key, index) => new SmartBpRecognizedPlayerCharacterSlot
            {
                Index = index,
                CharacterName = key,
                SlotState = SmartBpRecognizedSlotState.Selected,
                RecognitionConfidence = .99,
                IsAutoApplySafe = true,
                RecognitionReason = "already aligned distribution evidence"
            }).ToList();

            var trigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                fixture.Guidance.GetRuntimeSnapshot(),
                observed,
                fixture.Selection.GetCurrentBpSlotCommitState());
            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.False(trigger.ShouldReconcile);
            Assert.Equal(0, result.CharacterApplyResult.AppliedCount);
            Assert.False(result.GuidanceResult.Moved);
            Assert.Contains(result.Diagnostics, message => message.Contains("already match", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task ManualForceSyncAppliesCharactersWhenGuidanceIsAmbiguous()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: -1);
            var service = CreateReconciliation(fixture);
            var observed = State("未知", pickedSur: [(0, "a"), (1, "b")]);

            var result = await service.ReconcileAsync(observed, SmartBpReconciliationMode.ManualForceSync);

            Assert.True(result.CharacterApplyResult.AppliedCount == 2, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal("A", fixture.Game.SurPlayerList[0].Character?.Name);
            Assert.Equal("B", fixture.Game.SurPlayerList[1].Character?.Name);
            Assert.False(result.GuidanceResult.Succeeded);
        });
    }

    [Fact]
    public void CharacterOperationsUseCanonicalNameRatherThanImageFileName()
    {
        var fixture = CreateHost(GameProgress.Game1FirstHalf);
        var resolved = new SmartBpCharacterResolver(fixture.Selection).Resolve("a", Camp.Sur, 0, .99);

        Assert.Equal("A", resolved.ResolvedCharacterName);
        Assert.NotEqual("a.png", resolved.ResolvedCharacterName);
    }

    [Theory]
    [InlineData(SmartBpDetectedOperationApplyMode.CurrentStep, true)]
    [InlineData(SmartBpDetectedOperationApplyMode.AutomaticSupplement, true)]
    [InlineData(SmartBpDetectedOperationApplyMode.FreeSync, false)]
    public async Task ApplyModeControlsTransitionAnimation(
        SmartBpDetectedOperationApplyMode applyMode,
        bool expectedAnimation)
    {
        var game = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Game1FirstHalf);
        var character = new Character("A", Camp.Sur, "a.png");
        var slotIndex = applyMode == SmartBpDetectedOperationApplyMode.CurrentStep ? 1 : 0;
        int? sourceStepIndex = applyMode switch
        {
            SmartBpDetectedOperationApplyMode.CurrentStep => 1,
            SmartBpDetectedOperationApplyMode.AutomaticSupplement => 0,
            _ => null
        };
        var sourceIndexes = new[] { slotIndex };
        var workflow = new[]
        {
            new GameGuidanceStepSnapshot(0, GameAction.PickSur, [0], null),
            new GameGuidanceStepSnapshot(1, GameAction.PickSur, [1], null)
        };
        var selection = new Mock<ICharacterSelectionService>();
        selection
            .Setup(service => service.SelectSurvivorAsync(slotIndex, character, expectedAnimation, true))
            .Returns(Task.CompletedTask);
        var guidance = new Mock<IGameGuidanceService>();
        guidance
            .Setup(service => service.GetRuntimeSnapshot())
            .Returns(new GameGuidanceRuntimeSnapshot(true, 1, GameAction.PickSur, [1], null, workflow));
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(service => service.CurrentGame).Returns(game);
        shared.SetupGet(service => service.SurCharaDict).Returns(
            new SortedDictionary<string, Character>(StringComparer.Ordinal) { [character.Name] = character });
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(service => service.Settings).Returns(new SmartBpRecognitionSettings
        {
            RecognitionVisualBufferMilliseconds = 0
        });
        var applier = new SmartBpDetectedOperationApplier(
            selection.Object,
            guidance.Object,
            shared.Object,
            settings.Object);
        var operation = new SmartBpDetectedOperation(
            SmartBpDetectedOperationKind.PickSurvivor,
            GameAction.PickSur,
            sourceIndexes,
            Camp.Sur,
            slotIndex,
            character.Name,
            character.Name,
            null,
            .99,
            "test",
            sourceStepIndex,
            applyMode);

        var result = await applier.ApplyAsync([operation]);

        Assert.Equal(1, result.AppliedCount);
        selection.Verify(
            service => service.SelectSurvivorAsync(slotIndex, character, expectedAnimation, true),
            Times.Once);
    }

    [Fact]
    public async Task ManualForceSyncFailureDoesNotPolluteNextAutomaticTick()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: -1);
            var service = CreateReconciliation(fixture);

            var failed = await service.ReconcileAsync(State("未知"), SmartBpReconciliationMode.ManualForceSync);
            var automatic = await service.ReconcileAsync(
                State("选择求生者", pickedSur: [(0, "c"), (1, "d")]),
                SmartBpReconciliationMode.Automatic);

            Assert.Equal(0, failed.CharacterApplyResult.AppliedCount);
            Assert.False(failed.GuidanceResult.Succeeded);
            Assert.Equal(2, automatic.CharacterApplyResult.AppliedCount);
            Assert.Equal("C", fixture.Game.SurPlayerList[0].Character?.Name);
            Assert.Equal("D", fixture.Game.SurPlayerList[1].Character?.Name);
        });
    }

    [Fact]
    public async Task ForceSyncReportsCharacterAndGuidanceResultsSeparately()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: -1);
            var result = await CreateReconciliation(fixture).ReconcileAsync(
                State("未知", pickedSur: [(0, "a")]),
                SmartBpReconciliationMode.ManualForceSync);

            Assert.Equal(1, result.CharacterApplyResult.AppliedCount);
            Assert.Equal(0, result.EmptyApplyResult.AppliedCount);
            Assert.False(result.GuidanceResult.Succeeded);
            Assert.Contains("Guidance held", result.GuidanceResult.Message);
        });
    }

    [Fact]
    public async Task UnknownObservationDoesNotClearHostCharacter()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: 0);
            await fixture.Selection.SelectSurvivorAsync(0, fixture.Characters["a"], false);
            var service = CreateReconciliation(fixture);
            var observed = State("选择求生者");

            await service.ReconcileAsync(observed, SmartBpReconciliationMode.ManualForceSync);

            Assert.Equal("A", fixture.Game.SurPlayerList[0].Character?.Name);
            Assert.Equal(BpSlotCommitState.CommittedCharacter, fixture.Game.BpSlotCommitState.SurvivorPicks[0]);
        });
    }

    [Fact]
    public async Task ExplicitEmptyObservationCommitsEmptyBan()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0], null)
            };
            var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
            var service = CreateReconciliation(fixture);
            var observed = State("选择求生者");
            observed.BannedSur[0] = new SmartBpRecognizedCharacterSlot
            {
                Index = 0,
                CharacterName = "未选择",
                SlotState = SmartBpRecognizedSlotState.Empty,
                RecognitionConfidence = .99,
                IsAutoApplySafe = true,
                RecognitionReason = "explicit empty label"
            };

            var result = await service.ReconcileAsync(observed, SmartBpReconciliationMode.ManualForceSync);

            Assert.True(result.EmptyApplyResult.AppliedCount == 1, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(BpSlotCommitState.CommittedEmpty, fixture.Game.BpSlotCommitState.SurvivorBans[0]);
        });
    }

    [Fact]
    public void CommittedEmptyIsDifferentFromPending()
    {
        var fixture = CreateHost(GameProgress.Game1FirstHalf);
        fixture.Game.BpSlotCommitState.SurvivorBans[0] = BpSlotCommitState.CommittedEmpty;

        var state = fixture.Selection.GetCurrentBpSlotCommitState();

        Assert.Equal(BpSlotCommitState.CommittedEmpty, state.SurvivorBans[0]);
        Assert.Equal(BpSlotCommitState.Pending, state.SurvivorBans[1]);
    }

    [Fact]
    public async Task CompletedCurrentPickSlotsDoNotGuessThatTheNextPickStepHasStarted()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: 0);
            await fixture.Selection.SelectSurvivorAsync(0, fixture.Characters["a"], false);
            await fixture.Selection.SelectSurvivorAsync(1, fixture.Characters["b"], false);
            var service = CreateReconciliation(fixture);

            var result = await service.ReconcileAsync(State("选择求生者"), SmartBpReconciliationMode.Automatic);

            Assert.Equal(0, result.GuidanceResult.TargetStepIndex);
            Assert.False(result.GuidanceResult.Moved);
            Assert.Equal(0, fixture.Guidance.NextStepCallCount);
        });
    }

    [Fact]
    public async Task CurrentConsecutivePickStepStaysPutWithoutNextSlotEvidence()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: 1);
            await fixture.Selection.SelectSurvivorAsync(0, fixture.Characters["a"], false);
            await fixture.Selection.SelectSurvivorAsync(1, fixture.Characters["b"], false);
            await fixture.Selection.SelectSurvivorAsync(2, fixture.Characters["c"], false);
            var service = CreateReconciliation(fixture);

            var result = await service.ReconcileAsync(State("选择求生者"), SmartBpReconciliationMode.Automatic);

            Assert.Equal(1, result.GuidanceResult.TargetStepIndex);
            Assert.False(result.GuidanceResult.Moved);
            Assert.Equal(0, fixture.Guidance.NextStepCallCount);
        });
    }

    [Fact]
    public async Task PhaseAloneCannotSkipEarlierIncompletePickStep()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: 0);
            var service = CreateReconciliation(fixture);

            var result = await service.ReconcileAsync(State("选择监管者"), SmartBpReconciliationMode.Automatic);

            Assert.False(result.GuidanceResult.Moved);
            Assert.Equal(0, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(0, fixture.Guidance.NextStepCallCount);
            Assert.Equal(0, fixture.Guidance.DirectMoveCallCount);
            Assert.Contains("earliest incomplete", result.GuidanceResult.Message);
        });
    }

    [Fact]
    public async Task AutomaticCorrectionDoesNotMovePastUnresolvedBusinessStep()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: 0);
            var result = await CreateReconciliation(fixture).ReconcileAsync(
                State("选择监管者"),
                SmartBpReconciliationMode.Automatic);

            Assert.False(result.GuidanceResult.Moved);
            Assert.Equal(0, fixture.Guidance.CurrentStepIndex);
        });
    }

    [Fact]
    public async Task AutomaticCorrectionDoesNotAdvancePastTheObservedSlotStep()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: -1);

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                State("选择求生者", pickedSur: [(0, "a"), (1, "b")]),
                SmartBpReconciliationMode.Automatic);

            Assert.Equal(2, result.CharacterApplyResult.AppliedCount);
            Assert.False(result.GuidanceResult.Moved);
            Assert.Equal(0, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(0, fixture.Guidance.NextStepCallCount);
        });
    }

    [Fact]
    public async Task AutomaticReconciliationStartsGuidanceBeforeApplyingCharacters()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0, 1], null)
            };
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: -1, guidanceStarted: false);
            var observed = State("屏蔽求生者");
            SetSelectedBan(observed, 0, "a");
            SetSelectedBan(observed, 1, "b");

            var result = await CreateReconciliation(fixture).ReconcileAsync(observed, SmartBpReconciliationMode.Automatic);

            Assert.True(fixture.Guidance.IsGuidanceStarted);
            Assert.Equal(2, result.CharacterApplyResult.AppliedCount);
            Assert.Equal("A", fixture.Game.CurrentSurBannedList[0]?.Name);
            Assert.Equal("B", fixture.Game.CurrentSurBannedList[1]?.Name);
        });
    }

    [Fact]
    public async Task GuidedCatchUpStartsGuidanceAndAdvancesEverySlotStepWithPickAnimations()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0, 1], null),
                new GameGuidanceStepSnapshot(2, GameAction.BanSur, [2], null),
                new GameGuidanceStepSnapshot(3, GameAction.PickSur, [2], null)
            };
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: -1, guidanceStarted: false);
            var observed = State("选择求生者", pickedSur: [(0, "a"), (1, "b")]);
            SetSelectedBan(observed, 0, "a");
            SetSelectedBan(observed, 1, "b");
            SetSelectedBan(observed, 2, "c");

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed,
                SmartBpReconciliationMode.Automatic);

            Assert.True(result.GuidanceResult.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(3, fixture.Guidance.CurrentStepIndex);
            Assert.Equal([0, 1, 2, 3], fixture.Guidance.VisitedSteps);
            Assert.Equal(3, fixture.Guidance.NextStepCallCount);
            Assert.Equal(0, fixture.Guidance.DirectMoveCallCount);
            Assert.Equal(5, result.CharacterApplyResult.AppliedCount);
            fixture.Transition.Verify(service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        });
    }

    [Fact]
    public async Task ForceSyncWritesCurrentFrameWithoutAnimationAndDirectlyTargetsFirstIncompletePhaseSlot()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.BanHun, [0, 1], null),
                new GameGuidanceStepSnapshot(2, GameAction.PickSur, [0, 1], null),
                new GameGuidanceStepSnapshot(3, GameAction.BanSur, [2], null),
                new GameGuidanceStepSnapshot(4, GameAction.BanSur, [3], null)
            };
            var fixture = CreateHost(GameProgress.Game2FirstHalf, workflow, currentStep: -1, guidanceStarted: false);
            var observed = State("屏蔽求生者", pickedSur: [(0, "a"), (1, "b")]);
            SetSelectedBan(observed, 0, "a");
            SetSelectedBan(observed, 1, "b");
            SetSelectedBan(observed, 2, "c");
            SetSelectedHunterBan(observed, 0, "h");
            SetSelectedHunterBan(observed, 1, "h2");

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed,
                SmartBpReconciliationMode.ManualForceSync);

            Assert.True(result.GuidanceResult.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(4, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(GameAction.BanSur, result.GuidanceResult.TargetAction);
            Assert.Equal([3], result.GuidanceResult.TargetIndexes);
            Assert.Equal(0, fixture.Guidance.NextStepCallCount);
            Assert.Equal(1, fixture.Guidance.DirectMoveCallCount);
            Assert.Equal(7, result.CharacterApplyResult.AppliedCount);
            fixture.Transition.Verify(service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Fact]
    public async Task AutomaticCurrentAndFutureEmptyBanSlotsRemainPending()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0, 1], null),
                new GameGuidanceStepSnapshot(2, GameAction.BanSur, [2], null)
            };
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 0);
            var observed = State("屏蔽求生者");
            SetExplicitEmptyBan(observed, 0);
            SetExplicitEmptyBan(observed, 1);
            SetExplicitEmptyBan(observed, 2);

            var result = await CreateReconciliation(fixture).ReconcileAsync(observed, SmartBpReconciliationMode.Automatic);

            Assert.Equal(0, result.EmptyApplyResult.AppliedCount);
            Assert.Equal(BpSlotCommitState.Pending, fixture.Game.BpSlotCommitState.SurvivorBans[0]);
            Assert.Equal(BpSlotCommitState.Pending, fixture.Game.BpSlotCommitState.SurvivorBans[1]);
            Assert.Equal(BpSlotCommitState.Pending, fixture.Game.BpSlotCommitState.SurvivorBans[2]);
        });
    }

    [Fact]
    public async Task ExplicitEmptyBanBeforeLaterSelectedSlotIsCommittedWithoutShifting()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0, 1], null)
            };
            var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
            var observed = State("屏蔽求生者");
            SetExplicitEmptyBan(observed, 0);
            SetSelectedBan(observed, 1, "b");

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.Equal(1, result.EmptyApplyResult.AppliedCount);
            Assert.Equal(1, result.CharacterApplyResult.AppliedCount);
            Assert.Equal(BpSlotCommitState.CommittedEmpty, fixture.Game.BpSlotCommitState.SurvivorBans[0]);
            Assert.Null(fixture.Game.CurrentSurBannedList[0]);
            Assert.Equal(BpSlotCommitState.CommittedCharacter, fixture.Game.BpSlotCommitState.SurvivorBans[1]);
            Assert.Equal("B", fixture.Game.CurrentSurBannedList[1]?.Name);
        });
    }

    [Fact]
    public async Task SelectedRoleUpgradesCommittedEmptyBanWithoutAdjacentRewind()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0, 1], null),
                new GameGuidanceStepSnapshot(2, GameAction.BanSur, [2], null),
                new GameGuidanceStepSnapshot(3, GameAction.PickSur, [2], null),
                new GameGuidanceStepSnapshot(4, GameAction.BanSur, [3], null),
                new GameGuidanceStepSnapshot(5, GameAction.PickSur, [3], null)
            };
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 5);
            await fixture.Selection.CommitEmptyBanAsync(Camp.Sur, 0, false);
            await fixture.Selection.CommitEmptyBanAsync(Camp.Sur, 1, false);
            await fixture.Selection.CommitEmptySurvivorPickAsync(0, false);
            await fixture.Selection.CommitEmptySurvivorPickAsync(1, false);
            await fixture.Selection.CommitEmptyBanAsync(Camp.Sur, 2, false);
            await fixture.Selection.CommitEmptySurvivorPickAsync(2, false);
            await fixture.Selection.CommitEmptyBanAsync(Camp.Sur, 3, false);
            var observed = State("屏蔽求生者");
            SetSelectedBan(observed, 3, "c");

            var trigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                fixture.Guidance.GetRuntimeSnapshot(),
                observed,
                fixture.Selection.GetCurrentBpSlotCommitState());
            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.True(trigger.ShouldReconcile);
            Assert.False(trigger.ShouldReviewHistory);
            Assert.False(trigger.ShouldRewind);
            Assert.Equal([4], trigger.CommittedEmptyCorrectionSteps.Select(step => step.StepIndex));
            Assert.Equal(1, result.CharacterApplyResult.AppliedCount);
            Assert.Equal(5, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(0, fixture.Guidance.PrevStepCallCount);
            Assert.Equal(0, fixture.Guidance.DirectMoveCallCount);
            Assert.Equal("C", fixture.Game.CurrentSurBannedList[3]?.Name);
            Assert.Equal(BpSlotCommitState.CommittedCharacter, fixture.Game.BpSlotCommitState.SurvivorBans[3]);
        });
    }

    [Fact]
    public async Task SelectedRoleNeverOverwritesCommittedCharacterDuringCorrection()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[] { new GameGuidanceStepSnapshot(0, GameAction.BanSur, [3], null) };
            var fixture = CreateHost(GameProgress.Game1SecondHalf, workflow, currentStep: 0);
            await fixture.Selection.BanCharacterAsync(Camp.Sur, 3, fixture.Characters["c"], false);
            var observed = State("屏蔽求生者");
            SetSelectedBan(observed, 3, "d");

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                observed, SmartBpReconciliationMode.Automatic);

            Assert.Equal(0, result.CharacterApplyResult.AppliedCount);
            Assert.Equal("C", fixture.Game.CurrentSurBannedList[3]?.Name);
            Assert.Equal(BpSlotCommitState.CommittedCharacter, fixture.Game.BpSlotCommitState.SurvivorBans[3]);
        });
    }

    [Fact]
    public async Task TalentPhaseRecoversVisibleSurvivorBansAndIgnoresFutureHunterPick()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var workflow = new[]
            {
                new GameGuidanceStepSnapshot(0, GameAction.BanSur, [0, 1], null),
                new GameGuidanceStepSnapshot(1, GameAction.PickSur, [0, 1], null),
                new GameGuidanceStepSnapshot(2, GameAction.BanSur, [2], null),
                new GameGuidanceStepSnapshot(3, GameAction.PickSur, [2], null),
                new GameGuidanceStepSnapshot(4, GameAction.BanSur, [3], null),
                new GameGuidanceStepSnapshot(5, GameAction.PickSur, [3], null),
                new GameGuidanceStepSnapshot(6, GameAction.PickSurTalent, [], null),
                new GameGuidanceStepSnapshot(7, GameAction.PickHun, [0], null)
            };
            var fixture = CreateHost(GameProgress.Game1FirstHalf, workflow, currentStep: 0);
            await fixture.Selection.BanCharacterAsync(Camp.Sur, 0, fixture.Characters["a"], false);
            await fixture.Selection.BanCharacterAsync(Camp.Sur, 1, fixture.Characters["b"], false);
            for (var index = 0; index < 4; index++)
                await fixture.Selection.SelectSurvivorAsync(index, fixture.Characters[new[] { "a", "b", "c", "d" }[index]], false);
            var observed = State("求生者选择天赋中");
            SetSelectedBan(observed, 2, "c");
            SetSelectedBan(observed, 3, "d");

            var result = await CreateReconciliation(fixture).ReconcileAsync(observed, SmartBpReconciliationMode.Automatic);

            Assert.Equal(2, result.CharacterApplyResult.AppliedCount);
            Assert.Equal("C", fixture.Game.CurrentSurBannedList[2]?.Name);
            Assert.Equal("D", fixture.Game.CurrentSurBannedList[3]?.Name);
            Assert.True(result.GuidanceResult.Moved);
            Assert.Equal(6, result.GuidanceResult.TargetStepIndex);
            Assert.Equal(BpSlotCommitState.Pending, fixture.Game.BpSlotCommitState.HunterPick);
        });
    }

    [Fact]
    public async Task GuidanceDoesNotJumpAcrossMissingStepWithoutEvidence()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: 0);
            await fixture.Selection.SelectSurvivorAsync(0, fixture.Characters["a"], false);
            await fixture.Selection.SelectSurvivorAsync(1, fixture.Characters["b"], false);
            var result = await CreateReconciliation(fixture).ReconcileAsync(
                State("选择监管者"),
                SmartBpReconciliationMode.Automatic);

            Assert.True(result.GuidanceResult.Moved);
            Assert.Equal(1, fixture.Guidance.CurrentStepIndex);
            Assert.Equal(1, fixture.Guidance.NextStepCallCount);
            Assert.Equal(0, fixture.Guidance.DirectMoveCallCount);
            Assert.Contains("earliest incomplete", result.GuidanceResult.Message);
        });
    }

    [Fact]
    public async Task MultipleSkippedStepsAreReconciledInWorkflowOrder()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: -1);
            var service = CreateReconciliation(fixture);
            var observed = State("选择求生者", pickedSur: [(0, "a"), (1, "b"), (2, "c"), (3, "d")]);

            var result = await service.ReconcileAsync(observed, SmartBpReconciliationMode.ManualForceSync);

            Assert.True(result.CharacterApplyResult.AppliedCount == 4, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(
                [BpSlotCommitState.CommittedCharacter, BpSlotCommitState.CommittedCharacter, BpSlotCommitState.CommittedCharacter, BpSlotCommitState.CommittedCharacter],
                fixture.Game.BpSlotCommitState.SurvivorPicks);
        });
    }

    [Fact]
    public void FrameBufferRemainsBoundedUnderSlowOcr()
    {
        var fixture = CreateHost(GameProgress.Game1FirstHalf);
        var buffer = new SmartBpFrameRingBuffer(fixture.Settings.Object, fixture.Shared.Object);
        var frame = FrozenFrame();
        var now = DateTimeOffset.Now;

        for (var sequence = 1; sequence <= 600; sequence++)
            buffer.AddFrame(sequence, frame, now.AddMilliseconds(sequence));

        Assert.True(buffer.Count <= buffer.Capacity);
        Assert.InRange(buffer.Capacity, 8, 256);
    }

    [Fact]
    public void HistoricalFrameBufferReturnsOnlyCurrentGameContext()
    {
        var fixture = CreateHost(GameProgress.Game1FirstHalf);
        var current = fixture.Game;
        fixture.Shared.SetupGet(service => service.CurrentGame).Returns(() => current);
        var buffer = new SmartBpFrameRingBuffer(fixture.Settings.Object, fixture.Shared.Object);
        var frame = FrozenFrame();
        var now = DateTimeOffset.Now;
        buffer.AddFrame(1, frame, now);

        current = new Game(current.SurTeam, current.HunTeam, GameProgress.Game2FirstHalf);
        buffer.AddFrame(2, frame, now.AddMilliseconds(1));

        var recent = buffer.GetRecentFrames(TimeSpan.FromMinutes(1));

        Assert.Single(recent);
        Assert.Equal(2, recent[0].Sequence);
        Assert.Equal(current.Guid, recent[0].GameGuid);
        Assert.Equal(current.GameProgress, recent[0].GameProgress);
    }

    [Fact]
    public async Task Game1PickedSurNeverLeaksIntoGame2()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = CreateHost(GameProgress.Game1FirstHalf, PickWorkflow(), currentStep: -1);
            var current = fixture.Game;
            fixture.Shared.SetupGet(service => service.CurrentGame).Returns(() => current);
            await fixture.Selection.SelectSurvivorAsync(0, fixture.Characters["a"], false);
            await fixture.Selection.SelectSurvivorAsync(1, fixture.Characters["b"], false);
            Assert.Contains(current.SurTeam.GlobalBannedSurRecordList, character => character?.Name == "A");
            Assert.Contains(current.SurTeam.GlobalBannedSurRecordList, character => character?.Name == "B");

            current = new Game(current.SurTeam, current.HunTeam, GameProgress.Game2FirstHalf);
            fixture.Shared.Raise(service => service.CurrentGameChanged += null, EventArgs.Empty);

            var result = await CreateReconciliation(fixture).ReconcileAsync(
                State("选择求生者", pickedSur: [(0, "c"), (1, "d")]),
                SmartBpReconciliationMode.ManualForceSync);

            Assert.Equal(2, result.CharacterApplyResult.AppliedCount);
            Assert.Equal(["C", "D"], current.SurPlayerList.Take(2).Select(player => player.Character?.Name));
            Assert.DoesNotContain(current.SurPlayerList, player => player.Character?.Name is "A" or "B");
            Assert.Contains(current.SurTeam.GlobalBannedSurRecordList, character => character?.Name == "A");
            Assert.Contains(current.SurTeam.GlobalBannedSurRecordList, character => character?.Name == "B");
        });
    }

    private static SmartBpReconciliationService CreateReconciliation(HostFixture fixture)
    {
        var resolver = new SmartBpCharacterResolver(fixture.Selection);
        var builder = new SmartBpCandidateOperationBuilder(resolver, fixture.Shared.Object, new SmartBpPlayerIdentityMatcher(fixture.Shared.Object));
        var applier = new SmartBpDetectedOperationApplier(
            fixture.Selection,
            fixture.Guidance,
            fixture.Shared.Object,
            fixture.Settings.Object);
        return new SmartBpReconciliationService(
            fixture.Shared.Object,
            fixture.Selection,
            fixture.Guidance,
            builder,
            applier,
            fixture.Settings.Object);
    }

    private static HostFixture CreateHost(
        GameProgress progress,
        IReadOnlyList<GameGuidanceStepSnapshot>? workflow = null,
        int currentStep = -1,
        bool guidanceStarted = true)
    {
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), progress);
        var characters = new Dictionary<string, Character>
        {
            ["a"] = new("A", Camp.Sur, "a.png"),
            ["b"] = new("B", Camp.Sur, "b.png"),
            ["c"] = new("C", Camp.Sur, "c.png"),
            ["d"] = new("D", Camp.Sur, "d.png"),
            ["h"] = new("H", Camp.Hun, "h.png"),
            ["h2"] = new("H2", Camp.Hun, "h2.png")
        };
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(service => service.CurrentGame).Returns(game);
        shared.SetupGet(service => service.SurCharaDict).Returns(new SortedDictionary<string, Character>(
            characters.Values.Where(character => character.Camp == Camp.Sur).ToDictionary(character => character.Name)));
        shared.SetupGet(service => service.HunCharaDict).Returns(new SortedDictionary<string, Character>(
            characters.Values.Where(character => character.Camp == Camp.Hun).ToDictionary(character => character.Name)));
        var transition = new Mock<IFrontedTransitionOrchestrator>();
        transition
            .Setup(service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<FrontedTransitionRequest, Func<Task>, CancellationToken>((_, commitAsync, _) => commitAsync());
        transition
            .Setup(service => service.RunMultiTargetTransitionAsync(
                It.IsAny<IReadOnlyList<FrontedTransitionRequest>>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<FrontedTransitionRequest>, Func<Task>, CancellationToken>((_, commitAsync, _) => commitAsync());
        var selection = new CharacterSelectionService(shared.Object, transition.Object, Mock.Of<IFrontedLayoutService>());
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(service => service.Settings).Returns(CreateSettings());
        var guidance = new FakeGuidance(workflow ?? PickWorkflow(), currentStep, guidanceStarted);
        return new(game, shared, selection, transition, settings, guidance, characters);
    }

    private static SmartBpRecognitionSettings CreateSettings(int lookBehindSteps = 2) => new()
    {
        EnableAutoApplyRecognition = true,
        EnableAutoGuidanceSync = true,
        OcrRecognitionIntervalMs = 3000,
        MinimumOcrRecognitionIntervalMs = 3000,
        RecognitionSamplingIntervalMilliseconds = 150,
        OcrBackfillLookBehindSteps = lookBehindSteps,
        RecognitionTransitionLookBehindMilliseconds = 800,
        RecognitionTransitionReplayMinimumConfidence = .95
    };

    private static void SetSelectedBan(SmartBpBusinessStateRecognitionResult state, int slot, string name)
    {
        state.BannedSur[slot].CharacterName = name;
        state.BannedSur[slot].SlotState = SmartBpRecognizedSlotState.Selected;
        state.BannedSur[slot].RecognitionConfidence = .99;
        state.BannedSur[slot].IsAutoApplySafe = true;
        state.BannedSur[slot].RecognitionReason = "test selected Ban";
    }

    private static void SetSelectedHunterBan(SmartBpBusinessStateRecognitionResult state, int slot, string name)
    {
        state.BannedHun[slot].CharacterName = name;
        state.BannedHun[slot].SlotState = SmartBpRecognizedSlotState.Selected;
        state.BannedHun[slot].RecognitionConfidence = .99;
        state.BannedHun[slot].IsAutoApplySafe = true;
        state.BannedHun[slot].RecognitionReason = "test selected hunter Ban";
    }

    private static void SetExplicitEmptyBan(SmartBpBusinessStateRecognitionResult state, int slot)
    {
        state.BannedSur[slot].CharacterName = "未选择";
        state.BannedSur[slot].SlotState = SmartBpRecognizedSlotState.Empty;
        state.BannedSur[slot].RecognitionConfidence = .99;
        state.BannedSur[slot].IsAutoApplySafe = true;
        state.BannedSur[slot].RecognitionReason = "test explicit empty Ban";
    }

    private static GameGuidanceStepSnapshot[] PickWorkflow() =>
    [
        new(0, GameAction.PickSur, [0, 1], null),
        new(1, GameAction.PickSur, [2], null),
        new(2, GameAction.PickSur, [3], null),
        new(3, GameAction.PickHun, [0], null)
    ];

    private static GameGuidanceStepSnapshot[] ConservativePickWorkflow() =>
    [
        new(0, GameAction.BanSur, [0, 1], null),
        new(1, GameAction.PickSur, [0, 1], null),
        new(2, GameAction.BanSur, [2], null),
        new(3, GameAction.PickSur, [2], null),
        new(4, GameAction.DistributeChara, [], null),
        new(5, GameAction.PickSurTalent, [], null),
        new(6, GameAction.BanHun, [0], null),
        new(7, GameAction.PickHun, [0], null)
    ];

    private static SmartBpBusinessStateRecognitionResult State(
        string phase,
        IReadOnlyList<(int Slot, string Key)>? pickedSur = null)
    {
        var state = new SmartBpBusinessStateRecognitionResult
        {
            Phase = phase,
            BannedSur = Enumerable.Range(0, 4).Select(index => new SmartBpRecognizedCharacterSlot { Index = index }).ToList(),
            BannedHun = Enumerable.Range(0, 2).Select(index => new SmartBpRecognizedCharacterSlot { Index = index }).ToList(),
            PickedSur = Enumerable.Range(0, 4).Select(index => new SmartBpRecognizedPlayerCharacterSlot { Index = index }).ToList(),
            PickedHun = new SmartBpRecognizedPlayerCharacterSlot { Index = 0 }
        };
        foreach (var (slot, key) in pickedSur ?? [])
        {
            state.PickedSur[slot].CharacterName = key;
            state.PickedSur[slot].SlotState = SmartBpRecognizedSlotState.Selected;
            state.PickedSur[slot].RecognitionConfidence = .99;
            state.PickedSur[slot].IsAutoApplySafe = true;
            state.PickedSur[slot].RecognitionReason = "test observation";
        }
        return state;
    }

    private static SmartBpOcrRecognitionResult OcrResult(SmartBpBusinessStateRecognitionResult state) => new()
    {
        Phase = new SmartBpPhaseRecognitionResult { Phase = state.Phase },
        BusinessState = state
    };

    private static BitmapSource FrozenFrame()
    {
        var frame = new WriteableBitmap(2, 2, 96, 96, PixelFormats.Bgra32, null);
        frame.Freeze();
        return frame;
    }

    private sealed record HostFixture(
        Game Game,
        Mock<ISharedDataService> Shared,
        CharacterSelectionService Selection,
        Mock<IFrontedTransitionOrchestrator> Transition,
        Mock<ISmartBpRecognitionSettingsService> Settings,
        FakeGuidance Guidance,
        IReadOnlyDictionary<string, Character> Characters);

    private sealed class FakeGuidance(
        IReadOnlyList<GameGuidanceStepSnapshot> workflow,
        int currentStep,
        bool guidanceStarted) : IGameGuidanceService
    {
        public int CurrentStepIndex { get; private set; } = currentStep;
        public List<int> VisitedSteps { get; } = [];
        public int NextStepCallCount { get; private set; }
        public int PrevStepCallCount { get; private set; }
        public int DirectMoveCallCount { get; private set; }
        public Action<int>? BeforeMove { get; set; }
        public bool IsGuidanceStarted { get; set; } = guidanceStarted;
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
            if (CurrentStepIndex < 0 && workflow.Count > 0)
            {
                CurrentStepIndex = workflow.Min(step => step.StepIndex);
                VisitedSteps.Add(CurrentStepIndex);
            }
            return Task.FromResult<string?>(null);
        }

        public Task<string?> NextStepAsync(bool isNavigatePageEnable = true)
        {
            NextStepCallCount++;
            return SetStepAsync(CurrentStepIndex + 1);
        }
        public Task<string?> PrevStepAsync(bool isNavigatePageEnable = true)
        {
            PrevStepCallCount++;
            return SetStepAsync(CurrentStepIndex - 1);
        }
        public void StopGuidance() => IsGuidanceStarted = false;
        public void CompleteGuidance(string reason = "SmartBpCharacterBpEnded") => IsGuidanceStarted = false;
        public GameGuidanceRuntimeSnapshot GetRuntimeSnapshot()
        {
            var current = workflow.FirstOrDefault(step => step.StepIndex == CurrentStepIndex);
            return new(IsGuidanceStarted, CurrentStepIndex, current?.Action, current?.Indexes ?? [], current?.Time, workflow);
        }

        public Task<string?> MoveToStepAsync(int stepIndex, bool isNavigatePageEnable = true)
        {
            DirectMoveCallCount++;
            return SetStepAsync(stepIndex);
        }

        private Task<string?> SetStepAsync(int stepIndex)
        {
            if (workflow.All(step => step.StepIndex != stepIndex))
                return Task.FromResult<string?>("invalid step");
            BeforeMove?.Invoke(stepIndex);
            CurrentStepIndex = stepIndex;
            VisitedSteps.Add(stepIndex);
            return Task.FromResult<string?>(null);
        }
    }
}
