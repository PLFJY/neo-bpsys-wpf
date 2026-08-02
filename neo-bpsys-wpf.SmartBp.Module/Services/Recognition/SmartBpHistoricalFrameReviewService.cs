using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 在确实触发落后追赶时，按工作流步骤回看同一对局上下文中的短时原始帧。
/// </summary>
internal sealed class SmartBpHistoricalFrameReviewService(
    ISmartBpFrameRingBuffer frameRingBuffer,
    ISmartBpOcrBpRecognitionService ocrRecognition,
    ICharacterSelectionService selection,
    ISmartBpRecognitionSettingsService settings)
{
    /// <summary>
    /// 使用当前帧之前的代表帧补充遗漏角色；不会提交业务状态，也不会覆盖当前帧明确角色。
    /// </summary>
    /// <param name="current">当前帧业务证据。</param>
    /// <param name="currentFrameSequence">当前帧序号。</param>
    /// <param name="guidance">当前引导工作流快照。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>补充后的临时证据和诊断信息。</returns>
    public async Task<SmartBpHistoricalFrameReviewResult> SupplementAsync(
        SmartBpBusinessStateRecognitionResult current,
        long currentFrameSequence,
        GameGuidanceRuntimeSnapshot guidance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(guidance);

        var diagnostics = new List<string>();
        var host = selection.GetCurrentBpSlotCommitState();
        var trigger = SmartBpCatchUpTriggerEvaluator.Evaluate(guidance, current, host);
        diagnostics.Add($"Historical review trigger: {trigger.Reason}; current={trigger.CurrentPosition?.ToString() ?? "none"}; target={trigger.TargetPosition?.ToString() ?? "none"}.");
        if (!trigger.ShouldReviewHistory || trigger.TargetStep is null)
        {
            diagnostics.Add("Historical review skipped: Action/Indexes are aligned and no earlier Pending/CommittedEmpty slot requires look-back; current-frame role corrections go directly to reconciliation.");
            return new(current, 0, 0, diagnostics);
        }

        var configuredStepCount = Math.Clamp(settings.Settings.OcrBackfillLookBehindSteps, 0, 20);
        if (configuredStepCount == 0)
        {
            diagnostics.Add("Historical review skipped: configured workflow look-behind step count is zero.");
            return new(current, 0, 0, diagnostics);
        }

        var firstReviewStep = Math.Max(0, trigger.TargetStep.StepIndex - configuredStepCount);
        var reviewSteps = guidance.Workflow
            .Where(step => step.StepIndex >= firstReviewStep && step.StepIndex < trigger.TargetStep.StepIndex)
            .Where(step => SmartBpCatchUpTriggerEvaluator.IsBusinessStep(step.Action))
            .Where(step => HasReviewableHostSlot(step, host))
            .OrderBy(step => step.StepIndex)
            .ToArray();
        if (reviewSteps.Length == 0)
        {
            diagnostics.Add($"Historical review skipped: the previous {configuredStepCount} workflow steps contain no Pending or CommittedEmpty business slot that can be supplemented.");
            return new(current, 0, 0, diagnostics);
        }

        var eligibleSlots = BuildEligibleSlots(reviewSteps, current, host);
        if (eligibleSlots.Count == 0)
        {
            diagnostics.Add("Historical review skipped: reviewable steps contain no Pending/CommittedEmpty slot that lacks current-frame selected evidence.");
            return new(current, 0, 0, diagnostics);
        }

        var configuredWindow = Math.Clamp(settings.Settings.RecognitionTransitionLookBehindMilliseconds, 100, 5000);
        var samplingCoverage = Math.Max(50, settings.Settings.RecognitionSamplingIntervalMilliseconds) *
                               (configuredStepCount + 2);
        var lookBehind = TimeSpan.FromMilliseconds(Math.Max(configuredWindow, samplingCoverage));
        var frames = frameRingBuffer.GetRecentFrames(lookBehind)
            .Where(frame => frame.Sequence < currentFrameSequence)
            .OrderBy(frame => frame.Sequence)
            .ToArray();
        if (frames.Length == 0)
        {
            diagnostics.Add("Historical review skipped: no earlier frame remains in the current game context buffer.");
            return new(current, 0, 0, diagnostics);
        }

        var representatives = SelectRepresentativeFrames(frames, reviewSteps.Length);
        var evidence = eligibleSlots.ToDictionary(slot => slot, _ => new List<HistoricalRoleEvidence>());
        var reviewedFrames = 0;
        for (var index = 0; index < representatives.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = reviewSteps[index];
            var frame = representatives[index];
            var region = GetRegion(step.Action);
            var recognized = await ocrRecognition.RecognizeAsync(
                frame.Frame,
                new SmartBpOcrRecognitionRequest(
                    [region],
                    IncludePhase: true,
                    ParseContext: new SmartBpOcrFieldParseContext
                    {
                        AuthoritativePhase = SmartBpAutomaticMapping.ToPhase(step.Action),
                        CurrentGuidanceAction = step.Action,
                        IsAutomaticMode = true
                    }),
                cancellationToken).ConfigureAwait(false);
            reviewedFrames++;
            if (!SmartBpAutomaticMapping.TryMapPhase(recognized.Phase.Phase, out var recognizedAction) ||
                recognizedAction != step.Action)
            {
                diagnostics.Add($"Historical review frame {frame.Sequence} rejected for step {step.StepIndex}: expected_action={step.Action}; recognized_phase={recognized.Phase.Phase}; recognized_action={recognizedAction}.");
                continue;
            }

            CollectEvidence(recognized.BusinessState, step, frame.Sequence, evidence);
            diagnostics.Add($"Historical review frame {frame.Sequence} aligned to step {step.StepIndex} {step.Action}[{string.Join(',', step.Indexes)}]; requested_region={region}.");
        }

        var supplemented = CloneState(current);
        var supplementedCount = 0;
        foreach (var (slot, candidates) in evidence)
        {
            var canonicalNames = candidates
                .Select(candidate => candidate.CanonicalName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (canonicalNames.Length == 0)
                continue;
            if (canonicalNames.Length > 1)
            {
                diagnostics.Add($"Historical review held {slot.Action}[{slot.Index}]: conflicting canonical roles=[{string.Join(',', canonicalNames)}].");
                continue;
            }

            var best = candidates
                .Where(candidate => string.Equals(candidate.CanonicalName, canonicalNames[0], StringComparison.Ordinal))
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenByDescending(candidate => candidate.FrameSequence)
                .First();
            ApplySupplement(supplemented, slot, best);
            supplementedCount++;
            diagnostics.Add($"Historical review supplemented step {slot.StepIndex} {slot.Action}[{slot.Index}]={best.CanonicalName} from frame {best.FrameSequence}; merge_mode=supplement-only.");
        }

        if (supplementedCount == 0)
            diagnostics.Add("Historical review completed without safe aligned supplemental role evidence; no slot was forced or overwritten.");
        return new(supplemented, reviewedFrames, supplementedCount, diagnostics);
    }

    private void CollectEvidence(
        SmartBpBusinessStateRecognitionResult historical,
        GameGuidanceStepSnapshot step,
        long frameSequence,
        IReadOnlyDictionary<WorkflowSlot, List<HistoricalRoleEvidence>> evidence)
    {
        var minimumConfidence = Math.Clamp(settings.Settings.RecognitionTransitionReplayMinimumConfidence, 0, 1);
        foreach (var slot in evidence.Keys.Where(slot => slot.StepIndex == step.StepIndex))
        {
            var observed = GetSlot(historical, slot);
            if (observed is null ||
                observed.SlotState != SmartBpRecognizedSlotState.Selected ||
                !observed.IsAutoApplySafe ||
                observed.RecognitionConfidence < minimumConfidence ||
                string.IsNullOrWhiteSpace(observed.CharacterName))
                continue;

            var camp = slot.Action is GameAction.BanHun or GameAction.PickHun ? Camp.Hun : Camp.Sur;
            var canonicalName = selection.ResolveCharacterName(observed.CharacterName, camp);
            if (canonicalName is null)
                continue;
            evidence[slot].Add(new(canonicalName, observed.RecognitionConfidence, frameSequence, observed.BoundingBox));
        }
    }

    private static IReadOnlyList<WorkflowSlot> BuildEligibleSlots(
        IReadOnlyList<GameGuidanceStepSnapshot> reviewSteps,
        SmartBpBusinessStateRecognitionResult current,
        BpSlotCommitStateSnapshot host)
    {
        var result = new List<WorkflowSlot>();
        foreach (var step in reviewSteps)
        {
            foreach (var index in GetStepSlotIndexes(step))
            {
                var slot = new WorkflowSlot(step.Action, index, step.StepIndex);
                if (!IsHostSupplementable(slot, host))
                    continue;
                if (GetSlot(current, slot)?.SlotState == SmartBpRecognizedSlotState.Selected)
                    continue;
                result.Add(slot);
            }
        }
        return result;
    }

    private static IReadOnlyList<SmartBpBufferedFrame> SelectRepresentativeFrames(
        IReadOnlyList<SmartBpBufferedFrame> frames,
        int desiredCount)
    {
        var count = Math.Min(frames.Count, desiredCount);
        if (count == 0)
            return [];
        if (frames.Count == count)
            return frames;

        var result = new List<SmartBpBufferedFrame>(count);
        for (var index = 0; index < count; index++)
        {
            var frameIndex = Math.Min(
                frames.Count - 1,
                (int)Math.Floor((index + 1d) * frames.Count / (count + 1d)));
            result.Add(frames[frameIndex]);
        }
        return result;
    }

    private static SmartBpRecognitionRegion GetRegion(GameAction action) => action switch
    {
        GameAction.BanSur => SmartBpRecognitionRegion.RightTop,
        GameAction.BanHun => SmartBpRecognitionRegion.LeftTop,
        GameAction.PickSur => SmartBpRecognitionRegion.LeftBottom,
        GameAction.PickHun => SmartBpRecognitionRegion.RightBottom,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Action has no character review region.")
    };

    private static IEnumerable<int> GetStepSlotIndexes(GameGuidanceStepSnapshot step) => step.Action switch
    {
        GameAction.BanSur or GameAction.BanHun or GameAction.PickSur => step.Indexes,
        GameAction.PickHun => [-1],
        _ => []
    };

    private static bool HasReviewableHostSlot(
        GameGuidanceStepSnapshot step,
        BpSlotCommitStateSnapshot host) => step.Action switch
    {
        GameAction.BanSur => step.Indexes.Any(index => IsSupplementable(host.SurvivorBans, index)),
        GameAction.BanHun => step.Indexes.Any(index => IsSupplementable(host.HunterBans, index)),
        GameAction.PickSur => step.Indexes.Any(index => IsSupplementable(host.SurvivorPicks, index)),
        GameAction.PickHun => host.HunterPick is BpSlotCommitState.Pending or BpSlotCommitState.CommittedEmpty,
        _ => false
    };

    private static bool IsHostSupplementable(WorkflowSlot slot, BpSlotCommitStateSnapshot host) => slot.Action switch
    {
        GameAction.BanSur => IsSupplementable(host.SurvivorBans, slot.Index),
        GameAction.BanHun => IsSupplementable(host.HunterBans, slot.Index),
        GameAction.PickSur => IsSupplementable(host.SurvivorPicks, slot.Index),
        GameAction.PickHun => host.HunterPick is BpSlotCommitState.Pending or BpSlotCommitState.CommittedEmpty,
        _ => false
    };

    private static bool IsSupplementable(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count &&
        states[index] is BpSlotCommitState.Pending or BpSlotCommitState.CommittedEmpty;

    private static SmartBpRecognizedCharacterSlot? GetSlot(
        SmartBpBusinessStateRecognitionResult state,
        WorkflowSlot slot) => slot.Action switch
    {
        GameAction.BanSur => state.BannedSur.FirstOrDefault(item => item.Index == slot.Index),
        GameAction.BanHun => state.BannedHun.FirstOrDefault(item => item.Index == slot.Index),
        GameAction.PickSur => state.PickedSur.FirstOrDefault(item => item.Index == slot.Index),
        GameAction.PickHun => state.PickedHun,
        _ => null
    };

    private static void ApplySupplement(
        SmartBpBusinessStateRecognitionResult state,
        WorkflowSlot slot,
        HistoricalRoleEvidence evidence)
    {
        var target = GetSlot(state, slot);
        if (target is null)
            return;
        target.CharacterName = evidence.CanonicalName;
        target.SlotState = SmartBpRecognizedSlotState.Selected;
        target.RecognitionConfidence = evidence.Confidence;
        target.IsAutoApplySafe = true;
        target.RecognitionReason = $"Supplemented from aligned historical frame {evidence.FrameSequence}; current frame did not provide an authoritative selected role.";
        target.BoundingBox = evidence.BoundingBox;
    }

    private static SmartBpBusinessStateRecognitionResult CloneState(SmartBpBusinessStateRecognitionResult source) => new()
    {
        Phase = source.Phase,
        BannedSur = source.BannedSur.Select(CloneCharacterSlot).ToList(),
        BannedHun = source.BannedHun.Select(CloneCharacterSlot).ToList(),
        PickedSur = source.PickedSur.Select(ClonePlayerSlot).ToList(),
        PickedHun = ClonePlayerSlot(source.PickedHun),
        DistributionEvidence = source.DistributionEvidence.Select(ClonePlayerSlot).ToList()
    };

    private static SmartBpRecognizedCharacterSlot CloneCharacterSlot(SmartBpRecognizedCharacterSlot source) => new()
    {
        Index = source.Index,
        CharacterName = source.CharacterName,
        SlotState = source.SlotState,
        RecognitionConfidence = source.RecognitionConfidence,
        IsAutoApplySafe = source.IsAutoApplySafe,
        RecognitionReason = source.RecognitionReason,
        BoundingBox = source.BoundingBox
    };

    private static SmartBpRecognizedPlayerCharacterSlot ClonePlayerSlot(SmartBpRecognizedPlayerCharacterSlot source) => new()
    {
        Index = source.Index,
        CharacterName = source.CharacterName,
        PlayerId = source.PlayerId,
        SlotState = source.SlotState,
        RecognitionConfidence = source.RecognitionConfidence,
        IsAutoApplySafe = source.IsAutoApplySafe,
        RecognitionReason = source.RecognitionReason,
        BoundingBox = source.BoundingBox
    };

    private sealed record WorkflowSlot(GameAction Action, int Index, int StepIndex);

    private sealed record HistoricalRoleEvidence(
        string CanonicalName,
        double Confidence,
        long FrameSequence,
        OpenCvSharp.Rect? BoundingBox);
}

/// <summary>一次短时历史帧回看的临时补充结果。</summary>
internal sealed record SmartBpHistoricalFrameReviewResult(
    SmartBpBusinessStateRecognitionResult State,
    int ReviewedFrameCount,
    int SupplementedSlotCount,
    IReadOnlyList<string> Diagnostics);
