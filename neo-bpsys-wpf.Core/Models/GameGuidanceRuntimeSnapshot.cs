using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>对局引导工作流单步的不可变快照。</summary>
public sealed record GameGuidanceStepSnapshot(int StepIndex, GameAction Action, IReadOnlyList<int> Indexes, int? Time);

/// <summary>当前对局引导运行时的不可变快照。</summary>
public sealed record GameGuidanceRuntimeSnapshot(bool IsStarted, int CurrentStepIndex, GameAction? CurrentAction,
    IReadOnlyList<int> CurrentIndexes, int? CurrentTime, IReadOnlyList<GameGuidanceStepSnapshot> Workflow);
