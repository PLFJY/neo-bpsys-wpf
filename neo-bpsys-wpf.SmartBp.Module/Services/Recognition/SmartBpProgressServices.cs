using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 根据完整 SmartBP 业务状态推断 GameGuidance 当前进度。
/// </summary>
internal sealed class SmartBpProgressInferenceService : ISmartBpProgressInferenceService
{
    private enum SlotEvidence { Unknown, Empty, Selected }

    private sealed record SlotField(IReadOnlyDictionary<int, SlotEvidence> Slots);

    private sealed record Evidence(
        string Phase,
        SlotField BannedSur,
        SlotField BannedHun,
        SlotField PickedSur,
        SlotField PickedHun);

    private sealed record StepEvaluation(
        double PreviousCompletion,
        double CurrentPlausibility,
        double FutureContradiction,
        double PhaseMatch,
        double DistanceScore,
        string Reason);

    /// <inheritdoc />
    public SmartBpProgressInferenceResult Infer(
        SmartBpBusinessStateRecognitionResult observed,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpProgressInferenceOptions? options = null)
    {
        var effective = options ?? new SmartBpProgressInferenceOptions(true, null, .70, .10);
        var diagnostics = new List<string>
        {
            $"Progress inference requested: phase={observed.Phase}; currentStep={guidanceSnapshot.CurrentStepIndex}; allowBackward={effective.AllowBackwardSync}; maxForward={effective.MaxForwardDistance?.ToString() ?? "none"}; minimumScore={effective.MinimumScore:0.00}; margin={effective.MinimumScoreMargin:0.00}."
        };

        if (!guidanceSnapshot.IsStarted || guidanceSnapshot.Workflow.Count == 0)
            return Empty(false, "GameGuidance is not started or workflow is empty.", diagnostics);

        var evidence = BuildEvidence(observed);
        diagnostics.Add(DescribeEvidence(evidence));

        var ordered = guidanceSnapshot.Workflow.OrderBy(step => step.StepIndex).ToArray();
        var candidates = new List<SmartBpProgressCandidateScore>();
        foreach (var step in ordered)
        {
            if (!effective.AllowBackwardSync && step.StepIndex < guidanceSnapshot.CurrentStepIndex)
            {
                diagnostics.Add($"Rejected step {DescribeStep(step)} because automatic mode does not allow backward movement.");
                continue;
            }

            if (effective.MaxForwardDistance is { } maxForward &&
                step.StepIndex > guidanceSnapshot.CurrentStepIndex + maxForward)
            {
                diagnostics.Add($"Rejected step {DescribeStep(step)} because it exceeds max forward distance {maxForward}.");
                continue;
            }

            var evaluation = Evaluate(step, ordered, evidence, guidanceSnapshot.CurrentStepIndex);
            var score =
                .30 * evaluation.PhaseMatch +
                .35 * evaluation.PreviousCompletion +
                .20 * evaluation.CurrentPlausibility +
                .10 * evaluation.DistanceScore -
                .25 * evaluation.FutureContradiction;
            score = Math.Clamp(score, 0, 1);
            var reason = $"{evaluation.Reason}; phase={evaluation.PhaseMatch:0.00}; previous={evaluation.PreviousCompletion:0.00}; current={evaluation.CurrentPlausibility:0.00}; distance={evaluation.DistanceScore:0.00}; futurePenalty={evaluation.FutureContradiction:0.00}";
            if (IsStrongPhaseMismatch(evidence, step.Action))
            {
                score = Math.Min(score, .45);
                reason += "; capped because observed phase strongly indicates another action";
            }
            candidates.Add(new(step.StepIndex, step.Action, step.Indexes, score, reason));
        }

        var sorted = candidates.OrderByDescending(item => item.Score).ThenBy(item => Math.Abs(item.StepIndex - guidanceSnapshot.CurrentStepIndex)).ToArray();
        if (sorted.Length == 0)
            return Empty(false, "No candidate steps were available after applying sync options.", diagnostics);

        var best = sorted[0];
        var second = sorted.Length > 1 ? sorted[1].Score : 0;
        diagnostics.AddRange(sorted.Select(item => $"candidate step {item.StepIndex} {item.Action} [{string.Join(",", item.Indexes)}]: score={item.Score:0.00}; {item.Reason}"));
        var confident = best.Score >= effective.MinimumScore && best.Score - second >= effective.MinimumScoreMargin;
        var finalReason = confident
            ? $"Selected step {best.StepIndex} {best.Action} [{string.Join(",", best.Indexes)}]."
            : $"Inference ambiguous: best step {best.StepIndex} score={best.Score:0.00}, second={second:0.00}.";
        diagnostics.Add(finalReason);
        return new(confident, best.StepIndex, best.Action, best.Indexes, best.Score, second, finalReason, sorted, diagnostics);
    }

    private static SmartBpProgressInferenceResult Empty(bool confident, string reason, IReadOnlyList<string> diagnostics) =>
        new(confident, null, null, [], 0, 0, reason, [], diagnostics);

    private static Evidence BuildEvidence(SmartBpBusinessStateRecognitionResult observed) =>
        new(
            observed.Phase,
            new(BuildSlots(observed.BannedSur)),
            new(BuildSlots(observed.BannedHun)),
            new(BuildSlots(observed.PickedSur)),
            new(BuildSlots([observed.PickedHun])));

    private static IReadOnlyDictionary<int, SlotEvidence> BuildSlots(IEnumerable<SmartBpRecognizedCharacterSlot> slots) =>
        slots.ToDictionary(slot => slot.Index, ClassifySlot);

    private static SlotEvidence ClassifySlot(SmartBpRecognizedCharacterSlot slot)
    {
        var name = slot.CharacterName;
        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name, "unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "null", StringComparison.OrdinalIgnoreCase))
            return SlotEvidence.Unknown;
        if (slot.RecognitionConfidence > 0 && slot.RecognitionConfidence < .20)
            return SlotEvidence.Unknown;
        return SmartBpBusinessStateParser.IsUnselected(name) ? SlotEvidence.Empty : SlotEvidence.Selected;
    }

    private static StepEvaluation Evaluate(
        GameGuidanceStepSnapshot candidate,
        IReadOnlyList<GameGuidanceStepSnapshot> workflow,
        Evidence evidence,
        int currentStepIndex)
    {
        var previousSteps = workflow.Where(step => step.StepIndex < candidate.StepIndex).ToArray();
        var futureSteps = workflow.Where(step => step.StepIndex > candidate.StepIndex).ToArray();
        var previous = Math.Pow(CompletionRatio(previousSteps, evidence), 2);
        var current = CurrentPlausibility(candidate, evidence);
        var future = FutureContradictionRatio(futureSteps, evidence);
        var phase = PhaseActionScore(evidence, candidate.Action);
        var distance = DistanceScore(candidate.StepIndex, currentStepIndex);
        var reason = $"candidate={DescribeStep(candidate)}";
        return new(previous, current, future, phase, distance, reason);
    }

    private static double CompletionRatio(IEnumerable<GameGuidanceStepSnapshot> steps, Evidence evidence)
    {
        var total = 0;
        var selected = 0;
        foreach (var step in steps)
        {
            foreach (var slot in EnumerateRequiredSlots(step, evidence))
            {
                if (slot == SlotEvidence.Unknown) continue;
                total++;
                if (slot == SlotEvidence.Selected) selected++;
            }
        }
        return total == 0 ? 1 : (double)selected / total;
    }

    private static double FutureContradictionRatio(IEnumerable<GameGuidanceStepSnapshot> steps, Evidence evidence)
    {
        var total = 0;
        var selected = 0;
        foreach (var step in steps)
        {
            foreach (var slot in EnumerateRequiredSlots(step, evidence))
            {
                if (slot == SlotEvidence.Unknown) continue;
                total++;
                if (slot == SlotEvidence.Selected) selected++;
            }
        }
        return total == 0 ? 0 : (double)selected / total;
    }

    private static double CurrentPlausibility(GameGuidanceStepSnapshot step, Evidence evidence)
    {
        if (step.Action == GameAction.DistributeChara)
            return PickedSurCompletion(evidence) >= .99 ? 1 : .40;
        if (step.Action == GameAction.PickSurTalent)
            return PickedSurCompletion(evidence) >= .75 ? 1 : .55;
        if (step.Action == GameAction.PickHunTalent)
            return evidence.PickedHun.Slots.TryGetValue(0, out var pickedHun) && pickedHun == SlotEvidence.Selected ? 1 : .45;
        if (step.Action == GameAction.EndGuidance)
            return IsPostBpPhase(evidence.Phase) ? 1 : .45;

        var slots = EnumerateRequiredSlots(step, evidence).ToArray();
        if (slots.Length == 0) return .50;
        var known = slots.Where(slot => slot != SlotEvidence.Unknown).ToArray();
        if (known.Length == 0) return .60;
        var selected = known.Count(slot => slot == SlotEvidence.Selected);
        var empty = known.Count(slot => slot == SlotEvidence.Empty);
        if (empty > 0 && selected > 0) return .95;
        if (empty > 0) return 1;
        return .35;
    }

    private static IEnumerable<SlotEvidence> EnumerateRequiredSlots(GameGuidanceStepSnapshot step, Evidence evidence)
    {
        var field = step.Action switch
        {
            GameAction.BanSur => evidence.BannedSur,
            GameAction.BanHun => evidence.BannedHun,
            GameAction.PickSur => evidence.PickedSur,
            GameAction.PickHun => evidence.PickedHun,
            _ => null
        };
        if (field == null) yield break;
        var indexes = step.Action == GameAction.PickHun ? [0] : step.Indexes;
        foreach (var index in indexes)
        {
            if (field.Slots.TryGetValue(index, out var slot))
                yield return slot;
            else
                yield return SlotEvidence.Unknown;
        }
    }

    private static double PhaseActionScore(Evidence evidence, GameAction action)
    {
        if (IsPostBpPhase(evidence.Phase))
            return action == GameAction.EndGuidance ? 1 : .15;
        if (evidence.Phase == "天赋已锁定")
            return action is GameAction.PickSurTalent or GameAction.PickHunTalent or GameAction.EndGuidance ? .85 : .20;
        if (evidence.Phase == "求生者选择角色中")
        {
            if (action == GameAction.DistributeChara)
                return PickedSurCompletion(evidence) >= .99 ? 1 : .55;
            if (action == GameAction.PickSur)
                return PickedSurCompletion(evidence) < .99 ? .85 : .35;
            return .20;
        }
        if (SmartBpAutomaticMapping.TryMapPhase(evidence.Phase, out var mapped))
            return mapped == action ? 1 : .20;
        return .40;
    }

    private static bool IsStrongPhaseMismatch(Evidence evidence, GameAction action)
    {
        if (IsPostBpPhase(evidence.Phase))
            return action != GameAction.EndGuidance;
        if (evidence.Phase == "天赋已锁定")
            return false;
        if (evidence.Phase == "求生者选择角色中")
            return action is not (GameAction.DistributeChara or GameAction.PickSur);
        return SmartBpAutomaticMapping.TryMapPhase(evidence.Phase, out var mapped) && mapped != action;
    }

    private static double PickedSurCompletion(Evidence evidence)
    {
        var known = evidence.PickedSur.Slots.Values.Where(slot => slot != SlotEvidence.Unknown).ToArray();
        return known.Length == 0 ? 0 : (double)known.Count(slot => slot == SlotEvidence.Selected) / known.Length;
    }

    private static double DistanceScore(int candidateStepIndex, int currentStepIndex)
    {
        if (currentStepIndex < 0) return .50;
        var distance = Math.Abs(candidateStepIndex - currentStepIndex);
        return distance switch
        {
            0 => 1,
            1 => .90,
            2 => .75,
            3 => .60,
            _ => .45
        };
    }

    private static bool IsPostBpPhase(string phase) =>
        phase is "即将进入区域选择" or "区域选择" or "求生者选择区域中" or "监管者选择区域中" or "等待游戏开始" or "加载中" or "对局中";

    private static string DescribeEvidence(Evidence evidence) =>
        $"Observed slots: banned_sur selected=[{SelectedIndexes(evidence.BannedSur)}]; banned_hun selected=[{SelectedIndexes(evidence.BannedHun)}]; picked_sur selected=[{SelectedIndexes(evidence.PickedSur)}]; picked_hun selected={evidence.PickedHun.Slots.TryGetValue(0, out var hun) && hun == SlotEvidence.Selected}.";

    private static string SelectedIndexes(SlotField field) =>
        string.Join(",", field.Slots.Where(item => item.Value == SlotEvidence.Selected).Select(item => item.Key).OrderBy(item => item));

    private static string DescribeStep(GameGuidanceStepSnapshot step) =>
        $"step={step.StepIndex} action={step.Action} indexes=[{string.Join(",", step.Indexes)}]";
}

/// <summary>
/// 执行 SmartBP 精确进度对齐检查和同步。
/// </summary>
internal sealed class SmartBpProgressSyncService(
    IGameGuidanceService guidance,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpProgressInferenceService inference,
    ISmartBpDebugLog? debugLog = null) : ISmartBpProgressSyncService
{
    /// <inheritdoc />
    public SmartBpProgressAlignmentResult CheckAlignment(
        SmartBpBusinessStateRecognitionResult observed,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpProgressInferenceOptions? options = null)
    {
        var result = inference.Infer(observed, guidanceSnapshot, options);
        if (!result.IsConfident)
            return new(false, true, false, result, "Inference ambiguous; no automatic movement.", result.Diagnostics);
        if (result.TargetStepIndex == guidanceSnapshot.CurrentStepIndex)
            return new(true, false, false, result, "Current guidance step matches inferred progress.", result.Diagnostics);
        var reason = $"Current guidance is step {guidanceSnapshot.CurrentStepIndex} {guidanceSnapshot.CurrentAction} [{string.Join(",", guidanceSnapshot.CurrentIndexes)}], but observed state fits step {result.TargetStepIndex} {result.TargetAction} [{string.Join(",", result.TargetIndexes)}].";
        return new(false, false, true, result, reason, result.Diagnostics);
    }

    /// <inheritdoc />
    public async Task<SmartBpProgressSyncResult> ForceSyncAsync(
        SmartBpBusinessStateRecognitionResult observed,
        SmartBpProgressSyncMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<string> { $"Force progress sync requested: mode={mode}." };
        var snapshot = guidance.GetRuntimeSnapshot();
        if (!snapshot.IsStarted)
        {
            var startMessage = await guidance.StartGuidance(settings.Settings.EnableAutoGuidancePageNavigation);
            diagnostics.Add(string.IsNullOrWhiteSpace(startMessage)
                ? "GameGuidance was not started; StartGuidance completed without a message."
                : $"GameGuidance was not started; StartGuidance completed with message: {startMessage}.");
            snapshot = guidance.GetRuntimeSnapshot();
            if (!snapshot.IsStarted || snapshot.Workflow.Count == 0)
            {
                var failureMessage = string.IsNullOrWhiteSpace(startMessage)
                    ? "GameGuidance could not be started for progress sync."
                    : startMessage;
                return Finish(Fail(snapshot.CurrentStepIndex, null, null, [], failureMessage, diagnostics));
            }
        }

        var options = CreateOptions(mode);
        var alignment = CheckAlignment(observed, snapshot, options);
        diagnostics.AddRange(alignment.Diagnostics);
        if (!alignment.Inference.IsConfident || alignment.Inference.TargetStepIndex is not { } targetStep)
            return Finish(Fail(snapshot.CurrentStepIndex, null, null, [], alignment.Reason, diagnostics));

        if (mode == SmartBpProgressSyncMode.AutomaticDiagnostic && targetStep <= snapshot.CurrentStepIndex)
            return Finish(Fail(snapshot.CurrentStepIndex, targetStep, alignment.Inference.TargetAction, alignment.Inference.TargetIndexes,
                "Automatic progress sync only moves forward.", diagnostics));

        if (targetStep == snapshot.CurrentStepIndex)
            return Finish(new(true, false, snapshot.CurrentStepIndex, targetStep, alignment.Inference.TargetAction,
                alignment.Inference.TargetIndexes, "Current GameGuidance step already matches inferred progress.", diagnostics));

        var moveError = await guidance.MoveToStepAsync(targetStep, settings.Settings.EnableAutoGuidancePageNavigation);
        diagnostics.Add($"MoveToStepAsync completed: result={(string.IsNullOrWhiteSpace(moveError) ? "OK" : moveError)}.");
        if (!string.IsNullOrWhiteSpace(moveError))
            return Finish(Fail(snapshot.CurrentStepIndex, targetStep, alignment.Inference.TargetAction, alignment.Inference.TargetIndexes, moveError, diagnostics));

        var message = $"GameGuidance moved to step {targetStep} {alignment.Inference.TargetAction} [{string.Join(",", alignment.Inference.TargetIndexes)}].";
        return Finish(new(true, true, snapshot.CurrentStepIndex, targetStep, alignment.Inference.TargetAction,
            alignment.Inference.TargetIndexes, message, diagnostics));
    }

    private SmartBpProgressSyncResult Finish(SmartBpProgressSyncResult result)
    {
        debugLog?.Write("ProgressSync", result.Message);
        foreach (var diagnostic in result.Diagnostics)
            debugLog?.Write("ProgressSync", diagnostic);
        return result;
    }

    private SmartBpProgressInferenceOptions CreateOptions(SmartBpProgressSyncMode mode) =>
        mode == SmartBpProgressSyncMode.Manual
            ? new(true, null, .55, .01)
            : new(false, settings.Settings.GuidanceSyncLookAheadSteps,
                settings.Settings.SmartBpProgressInferenceMinimumScore,
                settings.Settings.SmartBpProgressInferenceMinimumScoreMargin);

    private static SmartBpProgressSyncResult Fail(
        int? previous,
        int? target,
        GameAction? action,
        IReadOnlyList<int> indexes,
        string message,
        IReadOnlyList<string> diagnostics) =>
        new(false, false, previous, target, action, indexes, message, diagnostics);
}
