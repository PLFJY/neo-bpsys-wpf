using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class SmartBpDetectedOperationApplier(
    ICharacterSelectionService selection,
    IGameGuidanceService guidance,
    ISharedDataService shared,
    ISmartBpRecognitionSettingsService settings) : ISmartBpDetectedOperationApplier
{
    public async Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();
        var applied = 0;
        var skipped = 0;
        var failedDependencyGroups = new HashSet<string>(StringComparer.Ordinal);
        if (operations.Count == 0)
            return new(0, 0, ["No candidate operations to apply."]);
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.DependencyGroup is { } dependencyGroup && failedDependencyGroups.Contains(dependencyGroup))
            {
                skipped++;
                messages.Add($"Skipped: dependency group {dependencyGroup} was stopped by an earlier unsafe operation for {Describe(operation)}.");
                continue;
            }
            var snapshot = guidance.GetRuntimeSnapshot();
            if (!ValidateWorkflowSource(operation, snapshot, out var workflowError)) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: {workflowError} for {Describe(operation)}."); continue; }
            if (operation.Confidence < 0.90) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: low confidence for {Describe(operation)}."); continue; }
            var isEmptyCommit = operation.Kind is SmartBpDetectedOperationKind.CommitEmptyBan or
                SmartBpDetectedOperationKind.CommitEmptySurvivorPick or
                SmartBpDetectedOperationKind.CommitEmptyHunterPick;
            Character? character = null;
            if (!isEmptyCommit)
            {
                if (operation.ResolvedCharacterName == null) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: unresolved character for {Describe(operation)}."); continue; }
                var dictionary = operation.Camp == Camp.Sur ? shared.SurCharaDict : shared.HunCharaDict;
                if (!dictionary.TryGetValue(operation.ResolvedCharacterName, out character)) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: resolved character name no longer exists: {operation.ResolvedCharacterName}."); continue; }
            }
            var playAnimation = operation.ApplyMode != SmartBpDetectedOperationApplyMode.FreeSync;
            if (playAnimation && settings.Settings.RecognitionVisualBufferMilliseconds > 0)
                await Task.Delay(settings.Settings.RecognitionVisualBufferMilliseconds, cancellationToken);

            switch (operation.Kind)
            {
                case SmartBpDetectedOperationKind.BanCharacter:
                    if (!TryGetBanSlot(operation.Camp, operation.SlotIndex, out var banned))
                    {
                        skipped++;
                        messages.Add($"Skipped: invalid ban slot for {Describe(operation)}.");
                        continue;
                    }
                    var banCommitState = operation.Camp == Camp.Sur
                        ? selection.GetCurrentBpSlotCommitState().SurvivorBans[operation.SlotIndex]
                        : selection.GetCurrentBpSlotCommitState().HunterBans[operation.SlotIndex];
                    if (IsSameCharacter(banned, character) && banCommitState == BpSlotCommitState.CommittedCharacter)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same ban for {Describe(operation)}.");
                        continue;
                    }
                    await selection.BanCharacterAsync(operation.Camp, operation.SlotIndex, character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} BanCharacter {operation.Camp}[{operation.SlotIndex}] {character!.Name}");
                    break;
                case SmartBpDetectedOperationKind.CommitEmptyBan:
                    var banStates = operation.Camp == Camp.Sur
                        ? selection.GetCurrentBpSlotCommitState().SurvivorBans
                        : selection.GetCurrentBpSlotCommitState().HunterBans;
                    if (operation.SlotIndex < 0 || operation.SlotIndex >= banStates.Count)
                    {
                        skipped++;
                        continue;
                    }
                    if (banStates[operation.SlotIndex] == BpSlotCommitState.CommittedEmpty)
                    {
                        skipped++;
                        messages.Add($"Skipped: host already contains explicit empty Ban for {Describe(operation)}.");
                        continue;
                    }
                    await selection.CommitEmptyBanAsync(operation.Camp, operation.SlotIndex, playAnimation);
                    messages.Add($"Applied explicit empty Ban {operation.Camp}[{operation.SlotIndex}].");
                    break;
                case SmartBpDetectedOperationKind.PickSurvivor:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        MarkDependencyFailed(operation, failedDependencyGroups);
                        messages.Add($"Skipped: invalid survivor slot for {Describe(operation)}.");
                        continue;
                    }
                    if (operation.RequireEmptySurvivorSlot && shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character != null)
                    {
                        skipped++;
                        MarkDependencyFailed(operation, failedDependencyGroups);
                        messages.Add($"Skipped: survivor recovery target is no longer empty for {Describe(operation)}.");
                        continue;
                    }
                    if (IsSameCharacter(shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character, character) &&
                        selection.GetCurrentBpSlotCommitState().SurvivorPicks[operation.SlotIndex] == BpSlotCommitState.CommittedCharacter)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SelectSurvivorAsync(operation.SlotIndex, character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} PickSurvivor Sur[{operation.SlotIndex}] {character!.Name}");
                    break;
                case SmartBpDetectedOperationKind.PickHunter:
                    if (IsSameCharacter(shared.CurrentGame.HunPlayer.Character, character) &&
                        selection.GetCurrentBpSlotCommitState().HunterPick == BpSlotCommitState.CommittedCharacter)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SelectHunterAsync(character, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} PickHunter {character!.Name}");
                    break;
                case SmartBpDetectedOperationKind.CommitEmptySurvivorPick:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        continue;
                    }
                    await selection.CommitEmptySurvivorPickAsync(operation.SlotIndex, playAnimation);
                    messages.Add($"Applied explicit empty survivor Pick Sur[{operation.SlotIndex}].");
                    break;
                case SmartBpDetectedOperationKind.CommitEmptyHunterPick:
                    await selection.CommitEmptyHunterPickAsync(playAnimation);
                    messages.Add("Applied explicit empty hunter Pick.");
                    break;
                case SmartBpDetectedOperationKind.SwapSurvivors:
                    if (operation.SlotIndex is < 0 or >= 4)
                    {
                        skipped++;
                        MarkDependencyFailed(operation, failedDependencyGroups);
                        messages.Add($"Skipped: invalid survivor swap target for {Describe(operation)}.");
                        continue;
                    }
                    if (IsSameCharacter(shared.CurrentGame.SurPlayerList[operation.SlotIndex].Character, character))
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op same character for {Describe(operation)}.");
                        continue;
                    }
                    var sourceMatch = shared.CurrentGame.SurPlayerList
                        .Select((player, index) => (player, index))
                        .FirstOrDefault(x => IsSameCharacter(x.player.Character, character));
                    if (sourceMatch.player == null) { skipped++; MarkDependencyFailed(operation, failedDependencyGroups); messages.Add($"Skipped: no source slot contains target character for {Describe(operation)}."); continue; }
                    var source = sourceMatch.index;
                    if (source == operation.SlotIndex)
                    {
                        skipped++;
                        messages.Add($"Skipped: no-op swap source and target are the same for {Describe(operation)}.");
                        continue;
                    }
                    await selection.SwapSurvivorsAsync(source, operation.SlotIndex, playAnimation);
                    messages.Add($"{AppliedPrefix(operation)} SwapSurvivors source={source} target={operation.SlotIndex} {character!.Name}");
                    break;
            }
            applied++;
        }
        return new(applied, skipped, messages);
    }

    private static bool ValidateWorkflowSource(
        SmartBpDetectedOperation operation,
        GameGuidanceRuntimeSnapshot snapshot,
        out string error)
    {
        if (operation.ApplyMode == SmartBpDetectedOperationApplyMode.FreeSync)
        {
            var valid = operation.Kind switch
            {
                SmartBpDetectedOperationKind.BanCharacter => operation.Camp is Camp.Sur or Camp.Hun && operation.SlotIndex >= 0,
                SmartBpDetectedOperationKind.CommitEmptyBan => operation.Camp is Camp.Sur or Camp.Hun && operation.SlotIndex >= 0,
                SmartBpDetectedOperationKind.PickSurvivor => operation.Camp == Camp.Sur && operation.SlotIndex is >= 0 and < 4,
                SmartBpDetectedOperationKind.CommitEmptySurvivorPick => operation.Camp == Camp.Sur && operation.SlotIndex is >= 0 and < 4,
                SmartBpDetectedOperationKind.PickHunter => operation.Camp == Camp.Hun && operation.SlotIndex == -1,
                SmartBpDetectedOperationKind.CommitEmptyHunterPick => operation.Camp == Camp.Hun && operation.SlotIndex == -1,
                _ => false
            };
            error = valid ? "" : "invalid free-sync operation contract";
            return valid;
        }
        if (operation.ApplyMode == SmartBpDetectedOperationApplyMode.CurrentStep)
        {
            if (snapshot.CurrentAction is not { } currentAction)
            {
                error = "current GameGuidance action is unavailable";
                return false;
            }
            var currentPosition = new SmartBpWorkflowPosition(currentAction, snapshot.CurrentIndexes);
            var sourcePosition = new SmartBpWorkflowPosition(
                operation.SourceGuidanceAction,
                operation.SourceGuidanceIndexes);
            if (!currentPosition.Equals(sourcePosition))
            {
                error = $"GameGuidance position changed from {sourcePosition} to {currentPosition}";
                return false;
            }
            if (operation.SourceWorkflowStepIndex is { } sourceStep && snapshot.CurrentStepIndex != sourceStep)
            {
                error = $"GameGuidance step changed from {sourceStep} to {snapshot.CurrentStepIndex}";
                return false;
            }
            error = "";
            return true;
        }

        if (operation.ApplyMode == SmartBpDetectedOperationApplyMode.AutomaticSupplement)
        {
            if (operation.SourceWorkflowStepIndex is not { } sourceStepIndex)
            {
                error = "automatic supplement source step is unavailable";
                return false;
            }
            var sourceStep = snapshot.Workflow.FirstOrDefault(step => step.StepIndex == sourceStepIndex);
            if (sourceStep is null ||
                !new SmartBpWorkflowPosition(sourceStep.Action, sourceStep.Indexes).Equals(
                    new SmartBpWorkflowPosition(operation.SourceGuidanceAction, operation.SourceGuidanceIndexes)))
            {
                error = $"automatic supplement source position does not match workflow step {sourceStepIndex}";
                return false;
            }
            if (sourceStepIndex >= snapshot.CurrentStepIndex)
            {
                error = $"automatic supplement source step {sourceStepIndex} is not earlier than current step {snapshot.CurrentStepIndex}";
                return false;
            }
            var validKind = operation.Kind is SmartBpDetectedOperationKind.BanCharacter or
                SmartBpDetectedOperationKind.PickSurvivor or
                SmartBpDetectedOperationKind.PickHunter;
            error = validKind ? "" : "automatic supplement only accepts concrete character operations";
            return validKind;
        }

        error = "unsupported operation apply mode";
        return false;
    }

    private static string AppliedPrefix(SmartBpDetectedOperation operation) => operation.ApplyMode switch
    {
        SmartBpDetectedOperationApplyMode.CurrentStep => "Guided catch-up with animation: Applied",
        SmartBpDetectedOperationApplyMode.AutomaticSupplement => "Automatic same-slot supplement with animation: Applied",
        _ => "Force-synced without animation: Applied"
    };

    private bool TryGetBanSlot(Camp camp, int slotIndex, out Character? character)
    {
        var list = camp == Camp.Sur ? shared.CurrentGame.CurrentSurBannedList : shared.CurrentGame.CurrentHunBannedList;
        if (slotIndex < 0 || slotIndex >= list.Count)
        {
            character = null;
            return false;
        }

        character = list[slotIndex];
        return true;
    }

    private static bool IsSameCharacter(Character? left, Character? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null) return false;
        return !string.IsNullOrWhiteSpace(left.Name) &&
               !string.IsNullOrWhiteSpace(right.Name) &&
               string.Equals(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static string Describe(SmartBpDetectedOperation operation) =>
        $"{operation.Kind} {operation.Camp}[{operation.SlotIndex}] {operation.RawCharacterName ?? "null"}";

    private static void MarkDependencyFailed(SmartBpDetectedOperation operation, ISet<string> failedDependencyGroups)
    {
        if (!string.IsNullOrWhiteSpace(operation.DependencyGroup))
            failedDependencyGroups.Add(operation.DependencyGroup);
    }
}
