using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 以主程序槽位提交状态和对局引导工作流为唯一业务状态，执行逐步追赶或强制同步。
/// </summary>
internal sealed class SmartBpReconciliationService(
    ISharedDataService shared,
    ICharacterSelectionService selection,
    IGameGuidanceService guidance,
    SmartBpCandidateOperationBuilder candidateBuilder,
    ISmartBpDetectedOperationApplier applier,
    ISmartBpRecognitionSettingsService settings) : ISmartBpReconciliationService
{
    /// <inheritdoc />
    public async Task<SmartBpReconciliationResult> ReconcileAsync(
        SmartBpBusinessStateRecognitionResult observed,
        SmartBpReconciliationMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observed);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<string>();
        var contextGuid = shared.CurrentGame.Guid;
        var contextProgress = shared.CurrentGame.GameProgress;
        var snapshot = guidance.GetRuntimeSnapshot();
        diagnostics.Add($"Reconciliation begin: mode={mode}; game={contextGuid}; progress={contextProgress}; phase={observed.Phase}; guidanceStep={snapshot.CurrentStepIndex}.");

        var mayStartGuidance = mode == SmartBpReconciliationMode.ManualForceSync ||
                               settings.Settings.EnableAutoGuidanceSync;
        var hasCurrentWorkflowStep = snapshot.Workflow.Any(step => step.StepIndex == snapshot.CurrentStepIndex);
        if ((!snapshot.IsStarted || !hasCurrentWorkflowStep) && mayStartGuidance)
        {
            var startResult = await guidance.StartGuidance(settings.Settings.EnableAutoGuidancePageNavigation);
            snapshot = guidance.GetRuntimeSnapshot();
            diagnostics.Add($"GameGuidance start requested: started={snapshot.IsStarted}; result={startResult ?? "OK"}.");
        }

        if (!snapshot.IsStarted || snapshot.Workflow.Count == 0)
        {
            var message = "GameGuidance is not started or its workflow is unavailable.";
            diagnostics.Add(message);
            return EmptyResult(snapshot.CurrentStepIndex, message, diagnostics);
        }

        return mode == SmartBpReconciliationMode.ManualForceSync
            ? await ForceSyncAsync(observed, snapshot, contextGuid, contextProgress, diagnostics, cancellationToken)
            : await CatchUpAsync(observed, snapshot, contextGuid, contextProgress, diagnostics, cancellationToken);
    }

    private async Task<SmartBpReconciliationResult> CatchUpAsync(
        SmartBpBusinessStateRecognitionResult observed,
        GameGuidanceRuntimeSnapshot initialSnapshot,
        Guid contextGuid,
        GameProgress contextProgress,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var trigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
            initialSnapshot,
            observed,
            selection.GetCurrentBpSlotCommitState());
        diagnostics.Add($"Automatic catch-up trigger: {trigger.Reason}; current={trigger.CurrentPosition?.ToString() ?? "none"}; target={trigger.TargetPosition?.ToString() ?? "none"}.");
        var target = trigger.TargetStep;
        if (target is null)
        {
            var message = $"Guided catch-up held: phase '{observed.Phase}' and current-frame slot evidence do not identify a workflow step.";
            diagnostics.Add(message);
            return EmptyResult(initialSnapshot.CurrentStepIndex, message, diagnostics);
        }

        var distributionPreview = target.Action == GameAction.DistributeChara
            ? candidateBuilder.BuildWithDiagnostics(observed, target.Action, target.Indexes)
            : null;
        var hasDistributionOperations = distributionPreview?.Operations.Count > 0;
        if (!trigger.ShouldReconcile && !hasDistributionOperations)
        {
            var message = $"Guided catch-up skipped: Action/Indexes already match {trigger.TargetPosition}, and no earlier host slot hole or new selected evidence requires reconciliation.";
            diagnostics.Add(message);
            return new(
                new(0, 0, []),
                new(0, 0, []),
                new(true, false, initialSnapshot.CurrentStepIndex, target.StepIndex, target.Action,
                    target.Indexes, message, diagnostics.ToArray()),
                diagnostics.ToArray());
        }

        if (hasDistributionOperations)
            diagnostics.Add($"Automatic catch-up trigger extended by {distributionPreview!.Operations.Count} concrete DistributeChara operation(s); already-aligned assignments do not retrigger.");

        diagnostics.Add($"Guided catch-up target resolved from current-frame slots: step={target.StepIndex}; action={target.Action}; indexes=[{string.Join(',', target.Indexes)}].");
        var characterApplied = 0;
        var characterSkipped = 0;
        var characterMessages = new List<string>();
        var emptyApplied = 0;
        var emptySkipped = 0;
        var emptyMessages = new List<string>();
        var moved = false;
        var previousStep = initialSnapshot.CurrentStepIndex;
        if (settings.Settings.EnableAutoApplyRecognition)
        {
            foreach (var step in trigger.CommittedEmptyCorrectionSteps
                         .Where(step => step.StepIndex < initialSnapshot.CurrentStepIndex))
            {
                var built = candidateBuilder.BuildWithDiagnostics(observed, step.Action, step.Indexes);
                var host = selection.GetCurrentBpSlotCommitState();
                var supplements = built.Operations
                    .Where(operation => !IsEmptyOperation(operation.Kind))
                    .Where(operation => TargetsCommittedEmpty(operation, host))
                    .Select(operation => operation with
                    {
                        SourceWorkflowStepIndex = step.StepIndex,
                        ApplyMode = SmartBpDetectedOperationApplyMode.AutomaticSupplement
                    })
                    .OrderBy(operation => operation.SlotIndex)
                    .ToArray();
                foreach (var operation in supplements)
                    await ApplyAndAccumulateAsync(operation, $"Automatic supplement step={step.StepIndex}");
            }
        }

        if (initialSnapshot.CurrentStepIndex > target.StepIndex)
        {
            if (!trigger.ShouldRewind)
            {
                var message = $"Guided catch-up waits in place: current step {initialSnapshot.CurrentStepIndex} is only {trigger.WorkflowStepDistance} step(s) after target {target.StepIndex}, or the target slot has no strong selected evidence. Waiting outranks automatic rewind.";
                diagnostics.Add(message);
                return Result(false, false, previousStep, target, message);
            }
            if (!settings.Settings.EnableAutoGuidanceSync)
            {
                var message = $"Guided rewind held: current step {initialSnapshot.CurrentStepIndex} is far ahead of strongly confirmed target {target.StepIndex}, but automatic GameGuidance synchronization is disabled.";
                diagnostics.Add(message);
                return Result(false, false, previousStep, target, message);
            }

            var rewindTargetStep = trigger.PendingEarlierSteps.FirstOrDefault()?.StepIndex ?? target.StepIndex;
            while (guidance.GetRuntimeSnapshot().CurrentStepIndex > rewindTargetStep)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureContext(contextGuid, contextProgress);
                var before = guidance.GetRuntimeSnapshot();
                var expectedPreviousStep = before.CurrentStepIndex - 1;
                var previousResult = await guidance.PrevStepAsync(settings.Settings.EnableAutoGuidancePageNavigation);
                var after = guidance.GetRuntimeSnapshot();
                if (after.CurrentStepIndex != expectedPreviousStep)
                {
                    var message = $"Guided correction failed to move backward exactly one step from {before.CurrentStepIndex} to {expectedPreviousStep}; actual={after.CurrentStepIndex}; result={previousResult ?? "null"}.";
                    diagnostics.Add(message);
                    return Result(false, moved, previousStep, target, message);
                }
                moved = true;
                diagnostics.Add($"Guided rewind moved backward one step: {before.CurrentStepIndex} -> {after.CurrentStepIndex}; far-ahead correction target={target.StepIndex}; earliest_unsatisfied_predecessor={rewindTargetStep}; direct positioning is not used.");
            }
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureContext(contextGuid, contextProgress);
            var snapshot = guidance.GetRuntimeSnapshot();
            var current = snapshot.Workflow.FirstOrDefault(step => step.StepIndex == snapshot.CurrentStepIndex);
            if (current is null)
            {
                var message = "Guided catch-up held: current GameGuidance step is unavailable.";
                diagnostics.Add(message);
                return Result(false, moved, previousStep, target, message);
            }
            if (current.StepIndex > target.StepIndex)
            {
                var message = $"Guided catch-up held: GameGuidance advanced past target step {target.StepIndex}.";
                diagnostics.Add(message);
                return Result(false, moved, previousStep, target, message);
            }

            if (SmartBpAutomaticMapping.IsCharacterOperationAction(current.Action) &&
                settings.Settings.EnableAutoApplyRecognition)
            {
                var built = candidateBuilder.BuildWithDiagnostics(observed, current.Action, current.Indexes);
                diagnostics.AddRange(built.Messages.Select(message => $"Step {current.StepIndex} {current.Action}: {message}"));
                var host = selection.GetCurrentBpSlotCommitState();
                var operations = built.Operations
                    .Where(operation => !IsEmptyOperation(operation.Kind) ||
                                        current.StepIndex < target.StepIndex ||
                                        IsExplicitEmptyHoleBeforeSelectedSlot(operation, current, observed))
                    .Where(operation => CanApplyToHostSlot(operation, host))
                    .Select(operation => operation with
                    {
                        SourceWorkflowStepIndex = current.StepIndex,
                        ApplyMode = SmartBpDetectedOperationApplyMode.CurrentStep
                    })
                    .OrderBy(operation => operation.SlotIndex)
                    .ToArray();

                foreach (var operation in operations)
                    await ApplyAndAccumulateAsync(operation,
                        $"Guided catch-up step={current.StepIndex}; slot={operation.Camp}[{operation.SlotIndex}]");
            }
            else if (SmartBpAutomaticMapping.IsCharacterOperationAction(current.Action))
            {
                diagnostics.Add($"Step {current.StepIndex} {current.Action}: automatic role application is disabled; only host slot state is inspected.");
            }

            if (current.StepIndex == target.StepIndex)
            {
                var message = moved
                    ? $"Guided catch-up reached step {target.StepIndex} {target.Action} [{string.Join(',', target.Indexes)}] one step at a time."
                    : "GameGuidance already points at the slot-derived current step.";
                diagnostics.Add(message);
                return Result(true, moved, previousStep, target, message);
            }

            var hostAfterApply = selection.GetCurrentBpSlotCommitState();
            if (SmartBpCatchUpTriggerEvaluator.IsBusinessStep(current.Action) &&
                !SmartBpCatchUpTriggerEvaluator.IsStepComplete(current, hostAfterApply))
            {
                var message = $"Guided catch-up held at earliest incomplete step {current.StepIndex} {current.Action} [{string.Join(',', current.Indexes)}]: main-program slot state is still incomplete.";
                diagnostics.Add(message);
                return Result(false, moved, previousStep, target, message);
            }
            if (!settings.Settings.EnableAutoGuidanceSync)
            {
                var message = $"Guided catch-up held at completed step {current.StepIndex}: automatic GameGuidance advancement is disabled.";
                diagnostics.Add(message);
                return Result(false, moved, previousStep, target, message);
            }

            var expectedNextStep = current.StepIndex + 1;
            var nextResult = await guidance.NextStepAsync(settings.Settings.EnableAutoGuidancePageNavigation);
            var nextSnapshot = guidance.GetRuntimeSnapshot();
            if (nextSnapshot.CurrentStepIndex != expectedNextStep)
            {
                var message = $"Guided catch-up failed to advance exactly one step from {current.StepIndex} to {expectedNextStep}; actual={nextSnapshot.CurrentStepIndex}; result={nextResult ?? "null"}.";
                diagnostics.Add(message);
                return Result(false, moved, previousStep, target, message);
            }
            moved = true;
            diagnostics.Add($"Guided catch-up advanced one step: {current.StepIndex} -> {nextSnapshot.CurrentStepIndex}; action={nextSnapshot.CurrentAction}; indexes=[{string.Join(',', nextSnapshot.CurrentIndexes)}].");
        }

        SmartBpReconciliationResult Result(
            bool succeeded,
            bool guidanceMoved,
            int priorStep,
            GameGuidanceStepSnapshot resolvedTarget,
            string message) =>
            new(
                new(characterApplied, characterSkipped, characterMessages),
                new(emptyApplied, emptySkipped, emptyMessages),
                new(succeeded, guidanceMoved, priorStep, resolvedTarget.StepIndex, resolvedTarget.Action,
                    resolvedTarget.Indexes, message, diagnostics.ToArray()),
                diagnostics.ToArray());

        async Task ApplyAndAccumulateAsync(SmartBpDetectedOperation operation, string diagnosticPrefix)
        {
            EnsureContext(contextGuid, contextProgress);
            var apply = await applier.ApplyAsync([operation], cancellationToken);
            if (IsEmptyOperation(operation.Kind))
            {
                emptyApplied += apply.AppliedCount;
                emptySkipped += apply.SkippedCount;
                emptyMessages.AddRange(apply.Messages);
            }
            else
            {
                characterApplied += apply.AppliedCount;
                characterSkipped += apply.SkippedCount;
                characterMessages.AddRange(apply.Messages);
            }
            diagnostics.AddRange(apply.Messages.Select(message => $"{diagnosticPrefix}; {message}"));
        }
    }

    private async Task<SmartBpReconciliationResult> ForceSyncAsync(
        SmartBpBusinessStateRecognitionResult observed,
        GameGuidanceRuntimeSnapshot snapshot,
        Guid contextGuid,
        GameProgress contextProgress,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var observedTarget = SmartBpCatchUpTriggerEvaluator.ResolveObservedTarget(
            snapshot,
            observed,
            selection.GetCurrentBpSlotCommitState());
        var operations = BuildForceSyncOperations(observed, snapshot, observedTarget);
        var characterApplied = 0;
        var characterSkipped = 0;
        var characterMessages = new List<string>();
        var emptyApplied = 0;
        var emptySkipped = 0;
        var emptyMessages = new List<string>();
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureContext(contextGuid, contextProgress);
            var apply = await applier.ApplyAsync([operation], cancellationToken);
            if (IsEmptyOperation(operation.Kind))
            {
                emptyApplied += apply.AppliedCount;
                emptySkipped += apply.SkippedCount;
                emptyMessages.AddRange(apply.Messages);
            }
            else
            {
                characterApplied += apply.AppliedCount;
                characterSkipped += apply.SkippedCount;
                characterMessages.AddRange(apply.Messages);
            }
            diagnostics.AddRange(apply.Messages.Select(applyMessage =>
                $"Force sync slot={operation.Camp}[{operation.SlotIndex}]; playAnimation=false; {applyMessage}"));
        }

        snapshot = guidance.GetRuntimeSnapshot();
        var target = ResolveHostTarget(snapshot, observed.Phase, selection.GetCurrentBpSlotCommitState());
        if (target is null)
        {
            var unresolvedMessage = $"Force sync applied current-frame roles without animation; Guidance held because phase '{observed.Phase}' does not identify a workflow step.";
            diagnostics.Add(unresolvedMessage);
            return new(
                new(characterApplied, characterSkipped, characterMessages),
                new(emptyApplied, emptySkipped, emptyMessages),
                new(false, false, snapshot.CurrentStepIndex, null, null, [], unresolvedMessage, diagnostics.ToArray()),
                diagnostics.ToArray());
        }

        var previousStep = snapshot.CurrentStepIndex;
        var moved = false;
        string message;
        if (target.StepIndex == previousStep)
        {
            message = $"Force sync wrote current-frame roles without animation; GameGuidance already points at step {target.StepIndex} {target.Action} [{string.Join(',', target.Indexes)}].";
        }
        else
        {
            EnsureContext(contextGuid, contextProgress);
            var moveResult = await guidance.MoveToStepAsync(target.StepIndex, settings.Settings.EnableAutoGuidancePageNavigation);
            var finalSnapshot = guidance.GetRuntimeSnapshot();
            moved = finalSnapshot.CurrentStepIndex == target.StepIndex;
            message = moved
                ? $"Force sync wrote current-frame roles without animation and directly positioned GameGuidance at step {target.StepIndex} {target.Action} [{string.Join(',', target.Indexes)}]."
                : $"Force sync wrote current-frame roles without animation, but direct GameGuidance positioning failed: {moveResult ?? "target step was not reached"}.";
        }
        diagnostics.Add(message);
        return new(
            new(characterApplied, characterSkipped, characterMessages),
            new(emptyApplied, emptySkipped, emptyMessages),
            new(!string.IsNullOrWhiteSpace(message) && (moved || target.StepIndex == previousStep), moved,
                previousStep, target.StepIndex, target.Action, target.Indexes, message, diagnostics.ToArray()),
            diagnostics.ToArray());
    }

    private IReadOnlyList<SmartBpDetectedOperation> BuildForceSyncOperations(
        SmartBpBusinessStateRecognitionResult observed,
        GameGuidanceRuntimeSnapshot snapshot,
        GameGuidanceStepSnapshot? observedTarget) =>
        new[]
        {
            candidateBuilder.BuildWithDiagnostics(observed, GameAction.BanSur, []).Operations,
            candidateBuilder.BuildWithDiagnostics(observed, GameAction.BanHun, []).Operations,
            candidateBuilder.BuildWithDiagnostics(observed, GameAction.PickSur, []).Operations,
            candidateBuilder.BuildWithDiagnostics(observed, GameAction.PickHun, []).Operations
        }
        .SelectMany(operations => operations)
        .Where(operation => !IsEmptyOperation(operation.Kind) ||
                            IsCompletedEmptyBeforeTarget(operation, snapshot, observedTarget))
        .Select(operation => operation with
        {
            SourceWorkflowStepIndex = null,
            ApplyMode = SmartBpDetectedOperationApplyMode.FreeSync
        })
        .OrderBy(operation => operation.SourceGuidanceAction)
        .ThenBy(operation => operation.SlotIndex)
        .ToArray();

    private static bool IsCompletedEmptyBeforeTarget(
        SmartBpDetectedOperation operation,
        GameGuidanceRuntimeSnapshot snapshot,
        GameGuidanceStepSnapshot? observedTarget)
    {
        if (!IsEmptyOperation(operation.Kind) || observedTarget is null)
            return false;

        var sourceStep = snapshot.Workflow
            .Where(step => StepContainsOperation(step, operation))
            .OrderBy(step => step.StepIndex)
            .FirstOrDefault();
        return sourceStep is not null && sourceStep.StepIndex < observedTarget.StepIndex;
    }

    private static bool StepContainsOperation(
        GameGuidanceStepSnapshot step,
        SmartBpDetectedOperation operation)
    {
        if (step.Action != operation.SourceGuidanceAction)
            return false;
        if (step.Action == GameAction.PickHun)
            return operation.SlotIndex == -1;
        return step.Indexes.Contains(operation.SlotIndex);
    }

    private static GameGuidanceStepSnapshot? ResolveHostTarget(
        GameGuidanceRuntimeSnapshot snapshot,
        string phase,
        BpSlotCommitStateSnapshot host)
    {
        if (!SmartBpAutomaticMapping.TryMapPhase(phase, out var action))
            return null;
        var candidates = snapshot.Workflow
            .Where(step => step.Action == action)
            .OrderBy(step => step.StepIndex)
            .ToArray();
        if (candidates.Length == 0)
            return null;
        if (!SmartBpCatchUpTriggerEvaluator.IsBusinessStep(action))
            return candidates.FirstOrDefault(step => step.StepIndex >= snapshot.CurrentStepIndex) ?? candidates[^1];
        return candidates.FirstOrDefault(step => !SmartBpCatchUpTriggerEvaluator.IsStepComplete(step, host)) ?? candidates[^1];
    }

    private static bool CanApplyToHostSlot(SmartBpDetectedOperation operation, BpSlotCommitStateSnapshot host) => operation.Kind switch
    {
        SmartBpDetectedOperationKind.BanCharacter =>
            operation.Camp == Camp.Sur
                ? IsWritableHole(host.SurvivorBans, operation.SlotIndex)
                : IsWritableHole(host.HunterBans, operation.SlotIndex),
        SmartBpDetectedOperationKind.CommitEmptyBan => operation.Camp == Camp.Sur
            ? IsPending(host.SurvivorBans, operation.SlotIndex)
            : IsPending(host.HunterBans, operation.SlotIndex),
        SmartBpDetectedOperationKind.PickSurvivor => IsWritableHole(host.SurvivorPicks, operation.SlotIndex),
        SmartBpDetectedOperationKind.CommitEmptySurvivorPick => IsPending(host.SurvivorPicks, operation.SlotIndex),
        SmartBpDetectedOperationKind.PickHunter =>
            host.HunterPick is BpSlotCommitState.Pending or BpSlotCommitState.CommittedEmpty,
        SmartBpDetectedOperationKind.CommitEmptyHunterPick => host.HunterPick == BpSlotCommitState.Pending,
        SmartBpDetectedOperationKind.SwapSurvivors => true,
        _ => false
    };

    private static bool IsPending(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count && states[index] == BpSlotCommitState.Pending;

    private static bool IsWritableHole(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count &&
        states[index] is BpSlotCommitState.Pending or BpSlotCommitState.CommittedEmpty;

    private static bool TargetsCommittedEmpty(
        SmartBpDetectedOperation operation,
        BpSlotCommitStateSnapshot host) => operation.Kind switch
    {
        SmartBpDetectedOperationKind.BanCharacter => operation.Camp == Camp.Sur
            ? IsCommittedEmpty(host.SurvivorBans, operation.SlotIndex)
            : IsCommittedEmpty(host.HunterBans, operation.SlotIndex),
        SmartBpDetectedOperationKind.PickSurvivor =>
            IsCommittedEmpty(host.SurvivorPicks, operation.SlotIndex),
        SmartBpDetectedOperationKind.PickHunter => host.HunterPick == BpSlotCommitState.CommittedEmpty,
        _ => false
    };

    private static bool IsCommittedEmpty(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count && states[index] == BpSlotCommitState.CommittedEmpty;

    private static bool IsEmptyOperation(SmartBpDetectedOperationKind kind) => kind is
        SmartBpDetectedOperationKind.CommitEmptyBan or
        SmartBpDetectedOperationKind.CommitEmptySurvivorPick or
        SmartBpDetectedOperationKind.CommitEmptyHunterPick;

    private static bool IsExplicitEmptyHoleBeforeSelectedSlot(
        SmartBpDetectedOperation operation,
        GameGuidanceStepSnapshot step,
        SmartBpBusinessStateRecognitionResult observed)
    {
        if (operation.Kind != SmartBpDetectedOperationKind.CommitEmptyBan ||
            operation.SourceGuidanceAction is not (GameAction.BanSur or GameAction.BanHun))
            return false;

        var laterIndexes = step.Indexes.Where(index => index > operation.SlotIndex).ToArray();
        var slots = operation.Camp == Camp.Sur ? observed.BannedSur : observed.BannedHun;
        return laterIndexes.Any(index => slots.FirstOrDefault(slot => slot.Index == index)?.SlotState ==
                                         SmartBpRecognizedSlotState.Selected);
    }

    private void EnsureContext(Guid expectedGuid, GameProgress expectedProgress)
    {
        if (shared.CurrentGame.Guid != expectedGuid || shared.CurrentGame.GameProgress != expectedProgress)
            throw new InvalidOperationException(
                $"Game context changed during reconciliation: expected={expectedGuid}/{expectedProgress}; actual={shared.CurrentGame.Guid}/{shared.CurrentGame.GameProgress}.");
    }

    private static SmartBpReconciliationResult EmptyResult(
        int previousStep,
        string message,
        IReadOnlyList<string> diagnostics,
        GameGuidanceStepSnapshot? target = null) =>
        new(
            new(0, 0, []),
            new(0, 0, []),
            new(false, false, previousStep, target?.StepIndex, target?.Action, target?.Indexes ?? [], message, diagnostics),
            diagnostics);
}
