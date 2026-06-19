using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>Immutable snapshot of one GameGuidance workflow step.</summary>
public sealed record GameGuidanceStepSnapshot(int StepIndex, GameAction Action, IReadOnlyList<int> Indexes, int? Time);

/// <summary>Immutable snapshot of the current GameGuidance runtime.</summary>
public sealed record GameGuidanceRuntimeSnapshot(bool IsStarted, int CurrentStepIndex, GameAction? CurrentAction,
    IReadOnlyList<int> CurrentIndexes, int? CurrentTime, IReadOnlyList<GameGuidanceStepSnapshot> Workflow);
