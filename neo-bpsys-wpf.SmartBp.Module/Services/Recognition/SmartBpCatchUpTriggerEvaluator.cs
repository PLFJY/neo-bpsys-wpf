using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 表示一个可按动作和槽位集合进行值比较的工作流位置。
/// </summary>
internal sealed class SmartBpWorkflowPosition : IEquatable<SmartBpWorkflowPosition>
{
    private readonly int[] _indexes;

    /// <summary>
    /// 初始化工作流位置。
    /// </summary>
    /// <param name="action">动作键。</param>
    /// <param name="indexes">动作涉及的槽位索引。</param>
    public SmartBpWorkflowPosition(GameAction action, IEnumerable<int> indexes)
    {
        Action = action;
        _indexes = indexes.Distinct().OrderBy(index => index).ToArray();
        Indexes = Array.AsReadOnly(_indexes);
    }

    /// <summary>获取动作键。</summary>
    public GameAction Action { get; }

    /// <summary>获取已规范化的槽位索引。</summary>
    public IReadOnlyList<int> Indexes { get; }

    /// <inheritdoc />
    public bool Equals(SmartBpWorkflowPosition? other) =>
        other is not null &&
        Action == other.Action &&
        _indexes.AsSpan().SequenceEqual(other._indexes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SmartBpWorkflowPosition);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Action);
        foreach (var index in _indexes)
            hash.Add(index);
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => $"{Action}[{string.Join(',', _indexes)}]";
}

/// <summary>
/// 描述一次自动追赶是否需要执行，以及是否需要读取历史帧。
/// </summary>
internal sealed record SmartBpCatchUpTriggerDecision(
    bool ShouldReconcile,
    bool ShouldReviewHistory,
    bool ShouldRewind,
    bool PositionMismatch,
    int WorkflowStepDistance,
    SmartBpWorkflowPosition? CurrentPosition,
    SmartBpWorkflowPosition? TargetPosition,
    GameGuidanceStepSnapshot? TargetStep,
    IReadOnlyList<GameGuidanceStepSnapshot> PendingEarlierSteps,
    IReadOnlyList<GameGuidanceStepSnapshot> CommittedEmptyCorrectionSteps,
    string Reason);

/// <summary>
/// 只读取当前画面证据、宿主槽位状态和 Guidance 快照，廉价判断是否需要追赶。
/// </summary>
internal static class SmartBpCatchUpTriggerEvaluator
{
    /// <summary>
    /// 评估自动追赶触发条件。
    /// </summary>
    /// <param name="snapshot">当前 Guidance 快照。</param>
    /// <param name="observed">当前帧证据。</param>
    /// <param name="host">主程序权威槽位提交状态。</param>
    /// <returns>追赶触发判定。</returns>
    public static SmartBpCatchUpTriggerDecision Evaluate(
        GameGuidanceRuntimeSnapshot snapshot,
        SmartBpBusinessStateRecognitionResult observed,
        BpSlotCommitStateSnapshot host)
    {
        var target = ResolveObservedTarget(snapshot, observed, host);
        var currentStep = snapshot.Workflow.FirstOrDefault(step => step.StepIndex == snapshot.CurrentStepIndex);
        var currentPosition = currentStep is null
            ? null
            : new SmartBpWorkflowPosition(currentStep.Action, currentStep.Indexes);
        var targetPosition = target is null
            ? null
            : new SmartBpWorkflowPosition(target.Action, target.Indexes);
        if (target is null)
        {
            return new(false, false, false, false, 0, currentPosition, null, null, [], [],
                $"phase '{observed.Phase}' does not resolve to a workflow position");
        }

        var positionMismatch = currentPosition is null || !currentPosition.Equals(targetPosition);
        var workflowStepDistance = currentStep is null ? 0 : currentStep.StepIndex - target.StepIndex;
        var pendingEarlierSteps = snapshot.Workflow
            .Where(step => step.StepIndex < target.StepIndex)
            .Where(step => IsBusinessStep(step.Action) && !IsStepComplete(step, host))
            .OrderBy(step => step.StepIndex)
            .ToArray();
        var correctionSteps = snapshot.Workflow
            .Where(step => step.StepIndex <= target.StepIndex)
            .Where(step => IsBusinessStep(step.Action) && HasCommittedEmptySelectedEvidence(step, observed, host))
            .OrderBy(step => step.StepIndex)
            .ToArray();
        var hasWritableTargetSelection = HasWritableSelectedEvidence(target, observed, host);
        var hasStrongTargetEvidence = HasSafeSelectedEvidence(target, observed);
        var shouldRewind = workflowStepDistance >= 2 &&
                           currentStep?.Action != target.Action &&
                           hasStrongTargetEvidence;
        var hasDistributionWork = target.Action == GameAction.DistributeChara &&
                                  observed.DistributionEvidence.Any(IsSafeSelected) &&
                                  host.SurvivorPicks.Any(state => state == BpSlotCommitState.Pending);
        var shouldReconcile = positionMismatch || pendingEarlierSteps.Length > 0 ||
                              hasWritableTargetSelection || correctionSteps.Length > 0 || hasDistributionWork;
        var shouldReview = shouldReconcile &&
                           workflowStepDistance <= 0 &&
                           (pendingEarlierSteps.Length > 0 ||
                            (positionMismatch && correctionSteps.Length == 0)) &&
                           target.StepIndex > 0;
        var reason = shouldReconcile
            ? $"triggered: position_mismatch={positionMismatch}; step_distance={workflowStepDistance}; strong_target_evidence={hasStrongTargetEvidence}; rewind={shouldRewind}; pending_earlier_steps=[{string.Join(',', pendingEarlierSteps.Select(step => step.StepIndex))}]; writable_target_selection={hasWritableTargetSelection}; committed_empty_corrections=[{string.Join(',', correctionSteps.Select(step => step.StepIndex))}]; distribution_work={hasDistributionWork}"
            : $"not triggered: current={currentPosition}; target={targetPosition}; no earlier slot hole and no new selected evidence for a Pending/CommittedEmpty slot";
        return new(shouldReconcile, shouldReview, shouldRewind, positionMismatch, workflowStepDistance, currentPosition, targetPosition,
            target, pendingEarlierSteps, correctionSteps, reason);
    }

    /// <summary>
    /// 根据当前画面和宿主完成状态定位画面所处的工作流步骤。
    /// </summary>
    /// <param name="snapshot">当前 Guidance 快照。</param>
    /// <param name="observed">当前帧证据。</param>
    /// <param name="host">主程序权威槽位提交状态。</param>
    /// <returns>槽位推导出的目标步骤；无法定位时返回 <see langword="null"/>。</returns>
    public static GameGuidanceStepSnapshot? ResolveObservedTarget(
        GameGuidanceRuntimeSnapshot snapshot,
        SmartBpBusinessStateRecognitionResult observed,
        BpSlotCommitStateSnapshot host)
    {
        if (!SmartBpAutomaticMapping.TryMapPhase(observed.Phase, out var action))
            return null;
        var candidates = snapshot.Workflow
            .Where(step => step.Action == action)
            .OrderBy(step => step.StepIndex)
            .ToArray();
        if (candidates.Length == 0)
            return null;
        if (!IsBusinessStep(action))
            return candidates.FirstOrDefault(step => step.StepIndex >= snapshot.CurrentStepIndex) ?? candidates[^1];

        var currentCompatibleStep = ResolveCurrentCompatibleStep(snapshot, candidates, observed, host);
        var latestObservedStep = candidates.LastOrDefault(step => IsStepObservedAnySelected(step, observed));
        var latestBridgeConfirmedStep = ResolveLatestBridgeConfirmedStep(snapshot, candidates, observed, host);
        return new[] { candidates[0], currentCompatibleStep, latestObservedStep, latestBridgeConfirmedStep }
            .Where(step => step is not null)
            .OrderByDescending(step => step!.StepIndex)
            .First()!;
    }

    /// <summary>判断步骤是否为宿主槽位提交型业务步骤。</summary>
    public static bool IsBusinessStep(GameAction action) => action is
        GameAction.BanSur or GameAction.BanHun or GameAction.PickSur or GameAction.PickHun;

    /// <summary>判断宿主是否已经明确提交步骤中的全部槽位。</summary>
    public static bool IsStepComplete(GameGuidanceStepSnapshot step, BpSlotCommitStateSnapshot host) => step.Action switch
    {
        GameAction.BanSur => step.Indexes.All(index => IsCommitted(host.SurvivorBans, index)),
        GameAction.BanHun => step.Indexes.All(index => IsCommitted(host.HunterBans, index)),
        GameAction.PickSur => step.Indexes.All(index => IsCommitted(host.SurvivorPicks, index)),
        GameAction.PickHun => host.HunterPick != BpSlotCommitState.Pending,
        _ => true
    };

    private static bool HasWritableSelectedEvidence(
        GameGuidanceStepSnapshot step,
        SmartBpBusinessStateRecognitionResult observed,
        BpSlotCommitStateSnapshot host) => step.Action switch
    {
        GameAction.BanSur => step.Indexes.Any(index => IsWritableHole(host.SurvivorBans, index) && IsSafeSelected(observed.BannedSur, index)),
        GameAction.BanHun => step.Indexes.Any(index => IsWritableHole(host.HunterBans, index) && IsSafeSelected(observed.BannedHun, index)),
        GameAction.PickSur => step.Indexes.Any(index => IsWritableHole(host.SurvivorPicks, index) && IsSafeSelected(observed.PickedSur, index)),
        GameAction.PickHun =>
            (host.HunterPick is BpSlotCommitState.Pending or BpSlotCommitState.CommittedEmpty) &&
            IsSafeSelected(observed.PickedHun),
        _ => false
    };

    private static bool HasCommittedEmptySelectedEvidence(
        GameGuidanceStepSnapshot step,
        SmartBpBusinessStateRecognitionResult observed,
        BpSlotCommitStateSnapshot host) => step.Action switch
    {
        GameAction.BanSur => step.Indexes.Any(index => IsCommittedEmpty(host.SurvivorBans, index) && IsSafeSelected(observed.BannedSur, index)),
        GameAction.BanHun => step.Indexes.Any(index => IsCommittedEmpty(host.HunterBans, index) && IsSafeSelected(observed.BannedHun, index)),
        GameAction.PickSur => step.Indexes.Any(index => IsCommittedEmpty(host.SurvivorPicks, index) && IsSafeSelected(observed.PickedSur, index)),
        GameAction.PickHun => host.HunterPick == BpSlotCommitState.CommittedEmpty && IsSafeSelected(observed.PickedHun),
        _ => false
    };

    private static bool HasSafeSelectedEvidence(
        GameGuidanceStepSnapshot step,
        SmartBpBusinessStateRecognitionResult observed) => step.Action switch
    {
        GameAction.BanSur => step.Indexes.Any(index => IsSafeSelected(observed.BannedSur, index)),
        GameAction.BanHun => step.Indexes.Any(index => IsSafeSelected(observed.BannedHun, index)),
        GameAction.PickSur => step.Indexes.Any(index => IsSafeSelected(observed.PickedSur, index)),
        GameAction.PickHun => IsSafeSelected(observed.PickedHun),
        _ => false
    };

    private static bool IsStepObservedAnySelected(
        GameGuidanceStepSnapshot step,
        SmartBpBusinessStateRecognitionResult observed) => step.Action switch
    {
        GameAction.BanSur => step.Indexes.Any(index => IsSelected(observed.BannedSur, index)),
        GameAction.BanHun => step.Indexes.Any(index => IsSelected(observed.BannedHun, index)),
        GameAction.PickSur => step.Indexes.Any(index => IsSelected(observed.PickedSur, index)),
        GameAction.PickHun => observed.PickedHun.SlotState == SmartBpRecognizedSlotState.Selected,
        _ => false
    };

    private static GameGuidanceStepSnapshot? ResolveLatestBridgeConfirmedStep(
        GameGuidanceRuntimeSnapshot snapshot,
        IReadOnlyList<GameGuidanceStepSnapshot> sameActionSteps,
        SmartBpBusinessStateRecognitionResult observed,
        BpSlotCommitStateSnapshot host)
    {
        GameGuidanceStepSnapshot? confirmed = null;
        for (var index = 1; index < sameActionSteps.Count; index++)
        {
            var previous = sameActionSteps[index - 1];
            var candidate = sameActionSteps[index];
            var bridge = snapshot.Workflow
                .Where(step => step.StepIndex >= previous.StepIndex && step.StepIndex < candidate.StepIndex)
                .Where(step => IsBusinessStep(step.Action))
                .ToArray();
            if (!bridge.Any(step => step.Action != candidate.Action))
                continue;
            if (bridge.All(step => IsStepComplete(step, host) || IsStepObservedComplete(step, observed)))
                confirmed = candidate;
        }
        return confirmed;
    }

    private static GameGuidanceStepSnapshot? ResolveCurrentCompatibleStep(
        GameGuidanceRuntimeSnapshot snapshot,
        IReadOnlyList<GameGuidanceStepSnapshot> sameActionSteps,
        SmartBpBusinessStateRecognitionResult observed,
        BpSlotCommitStateSnapshot host)
    {
        var current = sameActionSteps.LastOrDefault(step => step.StepIndex <= snapshot.CurrentStepIndex);
        if (current is null || current.Action != snapshot.CurrentAction)
            return null;

        var previous = sameActionSteps.LastOrDefault(step => step.StepIndex < current.StepIndex);
        if (previous is null)
            return current;

        var bridge = snapshot.Workflow
            .Where(step => step.StepIndex > previous.StepIndex && step.StepIndex < current.StepIndex)
            .Where(step => IsBusinessStep(step.Action) && step.Action != current.Action)
            .ToArray();
        return bridge.All(step => IsStepComplete(step, host) || IsStepObservedComplete(step, observed))
            ? current
            : previous;
    }

    private static bool IsStepObservedComplete(
        GameGuidanceStepSnapshot step,
        SmartBpBusinessStateRecognitionResult observed) => step.Action switch
    {
        GameAction.BanSur => step.Indexes.All(index => IsSafeSelected(observed.BannedSur, index)),
        GameAction.BanHun => step.Indexes.All(index => IsSafeSelected(observed.BannedHun, index)),
        GameAction.PickSur => step.Indexes.All(index => IsSafeSelected(observed.PickedSur, index)),
        GameAction.PickHun => IsSafeSelected(observed.PickedHun),
        _ => false
    };

    private static bool IsSelected(IEnumerable<SmartBpRecognizedCharacterSlot> slots, int index) =>
        slots.FirstOrDefault(slot => slot.Index == index)?.SlotState == SmartBpRecognizedSlotState.Selected;

    private static bool IsSafeSelected(IEnumerable<SmartBpRecognizedCharacterSlot> slots, int index) =>
        slots.FirstOrDefault(slot => slot.Index == index) is { } slot && IsSafeSelected(slot);

    private static bool IsSafeSelected(SmartBpRecognizedCharacterSlot slot) =>
        slot.SlotState == SmartBpRecognizedSlotState.Selected &&
        slot.IsAutoApplySafe &&
        slot.RecognitionConfidence >= .90;

    private static bool IsWritableHole(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count &&
        states[index] is BpSlotCommitState.Pending or BpSlotCommitState.CommittedEmpty;

    private static bool IsCommittedEmpty(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count && states[index] == BpSlotCommitState.CommittedEmpty;

    private static bool IsPending(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count && states[index] == BpSlotCommitState.Pending;

    private static bool IsCommitted(IReadOnlyList<BpSlotCommitState> states, int index) =>
        index >= 0 && index < states.Count && states[index] != BpSlotCommitState.Pending;
}
