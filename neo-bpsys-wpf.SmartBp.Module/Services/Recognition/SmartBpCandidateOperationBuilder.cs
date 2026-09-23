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

internal sealed class SmartBpCandidateOperationBuilder(
    ISmartBpCharacterResolver resolver,
    ISharedDataService shared,
    ISmartBpPlayerIdentityMatcher matcher)
{
    public IReadOnlyList<SmartBpDetectedOperation> Build(
        SmartBpBusinessStateRecognitionResult state,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
        => BuildWithDiagnostics(state, action, guidanceIndexes).Operations;

    public SmartBpCandidateOperationBuildResult BuildWithDiagnostics(
        SmartBpBusinessStateRecognitionResult state,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
    {
        if (!SmartBpAutomaticMapping.IsCharacterOperationAction(action))
            return new([], [$"Detected phase is a talent/lock phase ({state.Phase}); no character operation is generated."]);
        return action switch
        {
            GameAction.BanSur => BuildFromCharacterSlots(state.BannedSur, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.BanHun => BuildFromCharacterSlots(state.BannedHun, action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.PickSur => BuildFromPlayerSlots(state.PickedSur, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.PickSurvivor),
            GameAction.DistributeChara => BuildDistribution(
                state.DistributionEvidence.Count > 0 ? state.DistributionEvidence : state.PickedSur,
                guidanceIndexes),
            GameAction.PickHun => BuildFromPlayerSlots([state.PickedHun], action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.PickHunter, true),
            _ => new([], [$"Current GameGuidance action {action} is not a BP character operation."])
        };
    }

    public SmartBpCandidateOperationBuildResult BuildWithDiagnostics(
        SmartBpFocusedBusinessExtractionResult focused,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
    {
        if (!SmartBpAutomaticMapping.IsCharacterOperationAction(action))
            return new([], [$"Detected phase is a talent/lock phase ({focused.Phase}); no character operation is generated."]);
        return action switch
        {
            GameAction.BanSur => BuildFromCharacterSlots(focused.Slots, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.BanHun => BuildFromCharacterSlots(focused.Slots, action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.BanCharacter),
            GameAction.PickSur => BuildFromPlayerSlots(focused.Slots, action, guidanceIndexes, Camp.Sur, SmartBpDetectedOperationKind.PickSurvivor),
            GameAction.DistributeChara => BuildDistribution(focused.Slots, guidanceIndexes),
            GameAction.PickHun => focused.PickedHun == null
                ? new([], ["Focused picked_hun result did not contain picked_hun."])
                : BuildFromPlayerSlots([focused.PickedHun], action, guidanceIndexes, Camp.Hun, SmartBpDetectedOperationKind.PickHunter, true),
            _ => new([], [$"Current GameGuidance action {action} is not a BP character operation."])
        };
    }

    private SmartBpCandidateOperationBuildResult BuildFromCharacterSlots(IEnumerable<SmartBpRecognizedCharacterSlot> slots, GameAction action, IReadOnlyList<int> guidanceIndexes, Camp camp, SmartBpDetectedOperationKind kind)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var messages = new List<string>();
        foreach (var slot in slots)
        {
            if (guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(slot.Index))
            {
                messages.Add($"Skipped: index not in current GameGuidance indexes ({camp}[{slot.Index}] {slot.CharacterName}).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Unknown)
            {
                messages.Add($"Skipped: Unknown observation cannot modify host state ({camp}[{slot.Index}]).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Empty)
            {
                var emptyConfidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
                operations.Add(new(SmartBpDetectedOperationKind.CommitEmptyBan, action, guidanceIndexes.ToArray(), camp,
                    slot.Index, null, null, null, emptyConfidence,
                    slot.RecognitionReason ?? "Explicit empty Ban observation."));
                continue;
            }
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var confidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
            var resolved = resolver.Resolve(slot.CharacterName, camp, slot.Index, confidence);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, slot.Index, slot.CharacterName,
                resolved.ResolvedCharacterName, null, confidence,
                slot.RecognitionReason ?? $"Business-state snapshot phase {action} produced slot {slot.Index}."));
        }
        return new(operations, messages);
    }

    private SmartBpCandidateOperationBuildResult BuildFromPlayerSlots(IEnumerable<SmartBpRecognizedPlayerCharacterSlot> slots, GameAction action, IReadOnlyList<int> guidanceIndexes, Camp camp, SmartBpDetectedOperationKind kind, bool hunterSlot = false)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var messages = new List<string>();
        foreach (var slot in slots)
        {
            var internalSlot = hunterSlot ? -1 : slot.Index;
            if (!hunterSlot && guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(internalSlot))
            {
                messages.Add($"Skipped: index not in current GameGuidance indexes ({camp}[{internalSlot}] {slot.CharacterName}).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Unknown)
            {
                messages.Add($"Skipped: Unknown observation cannot modify host state ({camp}[{internalSlot}]).");
                continue;
            }
            if (slot.SlotState == SmartBpRecognizedSlotState.Empty)
            {
                var emptyKind = hunterSlot
                    ? SmartBpDetectedOperationKind.CommitEmptyHunterPick
                    : SmartBpDetectedOperationKind.CommitEmptySurvivorPick;
                var emptyConfidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
                operations.Add(new(emptyKind, action, guidanceIndexes.ToArray(), camp, internalSlot,
                    null, null, slot.PlayerId, emptyConfidence,
                    slot.RecognitionReason ?? "Explicit empty Pick observation."));
                continue;
            }
            if (SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var confidence = slot.IsAutoApplySafe ? slot.RecognitionConfidence : Math.Min(slot.RecognitionConfidence, .89);
            var resolved = resolver.Resolve(slot.CharacterName, camp, internalSlot, confidence);
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, internalSlot, slot.CharacterName,
                resolved.ResolvedCharacterName, slot.PlayerId, confidence,
                slot.RecognitionReason ?? (hunterSlot ? "Business-state snapshot mapped hunter visual slot 0 to internal hunter slot -1." : $"Business-state snapshot phase {action} produced slot {internalSlot}.")));
        }
        return new(operations, messages);
    }

    public IReadOnlyList<SmartBpDetectedOperation> Build(
        SmartBpFocusedExtractionResult extraction,
        GameAction action,
        IReadOnlyList<int> guidanceIndexes)
    {
        if (action == GameAction.DistributeChara)
            return BuildDistribution(extraction, guidanceIndexes);
        var operations = new List<SmartBpDetectedOperation>();
        var camp = action is GameAction.BanHun or GameAction.PickHun ? Camp.Hun : Camp.Sur;
        foreach (var slot in extraction.Slots)
        {
            if (slot.CharacterName == null || SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)) continue;
            var internalSlot = action == GameAction.PickHun ? -1 : slot.SlotIndex;
            if (action is GameAction.BanSur or GameAction.BanHun or GameAction.PickSur &&
                guidanceIndexes.Count > 0 && !guidanceIndexes.Contains(internalSlot))
                continue;
            if (action == GameAction.DistributeChara && internalSlot is < 0 or > 3) continue;
            var resolved = resolver.Resolve(slot.CharacterName, camp, internalSlot, slot.Confidence);
            var kind = action switch
            {
                GameAction.BanSur or GameAction.BanHun => SmartBpDetectedOperationKind.BanCharacter,
                GameAction.PickSur => SmartBpDetectedOperationKind.PickSurvivor,
                GameAction.PickHun => SmartBpDetectedOperationKind.PickHunter,
                GameAction.DistributeChara => SmartBpDetectedOperationKind.SwapSurvivors,
                _ => throw new NotSupportedException()
            };
            var reason = kind == SmartBpDetectedOperationKind.SwapSurvivors
                ? $"Place the detected character into fixed survivor player slot {internalSlot}."
                : $"Focused {action} extraction matched the authoritative guidance step.";
            operations.Add(new(kind, action, guidanceIndexes.ToArray(), camp, internalSlot,
                slot.CharacterName, resolved.ResolvedCharacterName,
                slot.PlayerId, slot.Confidence, reason));
        }
        return operations;
    }

    private SmartBpCandidateOperationBuildResult BuildDistribution(
        IEnumerable<SmartBpRecognizedPlayerCharacterSlot> slots,
        IReadOnlyList<int> guidanceIndexes)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var messages = new List<string>();
        var evidence = slots
            .Where(x => x.SlotState == SmartBpRecognizedSlotState.Selected)
            .Where(x => x.IsAutoApplySafe && x.RecognitionConfidence >= .95)
            .Where(x => !SmartBpBusinessStateParser.IsUnselected(x.CharacterName) && x.Index is >= 0 and < 4)
            .OrderBy(x => x.Index)
            .ToArray();
        var resolvedRoles = new List<(SmartBpRecognizedPlayerCharacterSlot Slot, SmartBpNormalizedCharacter Character, double Confidence)>();
        foreach (var slot in evidence)
        {
            var playerIdText = string.IsNullOrWhiteSpace(slot.PlayerId) ? "<missing>" : slot.PlayerId;
            messages.Add($"Distribution visual slot {slot.Index}: char={slot.CharacterName}, player_id={playerIdText}.");
            var confidence = slot.RecognitionConfidence;
            var resolved = resolver.Resolve(slot.CharacterName, Camp.Sur, slot.Index, confidence);
            if (resolved.ResolvedCharacterName == null)
            {
                messages.Add($"Skipped distribution visual slot {slot.Index}: unresolved character '{slot.CharacterName}'.");
                continue;
            }
            resolvedRoles.Add((slot, resolved, confidence));
        }

        var duplicateRoles = resolvedRoles
            .GroupBy(item => item.Character.ResolvedCharacterName!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (duplicateRoles.Count > 0)
            messages.Add($"Distribution recovery rejected duplicated role evidence=[{string.Join(',', duplicateRoles)}]; duplicated roles are not filled or shifted.");
        var uniqueRoles = resolvedRoles
            .Where(item => !duplicateRoles.Contains(item.Character.ResolvedCharacterName!))
            .ToArray();

        var simulated = shared.CurrentGame.SurPlayerList.Select(x => x.Character?.Name).ToArray();
        var missingRoles = uniqueRoles
            .Where(item => Array.FindIndex(simulated, name => string.Equals(
                name,
                item.Character.ResolvedCharacterName,
                StringComparison.Ordinal)) < 0)
            .OrderBy(item => item.Slot.Index)
            .ToArray();
        var recoveryGroup = missingRoles.Length > 0 ? $"distribution-recovery:{Guid.NewGuid():N}" : null;
        foreach (var item in missingRoles)
        {
            var emptySlot = Array.FindIndex(simulated, string.IsNullOrWhiteSpace);
            if (emptySlot < 0)
            {
                messages.Add($"Distribution recovery held {item.Character.ResolvedCharacterName}: no empty survivor slot remains; existing selections are preserved.");
                break;
            }
            operations.Add(new(SmartBpDetectedOperationKind.PickSurvivor, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, emptySlot, item.Slot.CharacterName,
                item.Character.ResolvedCharacterName, item.Slot.PlayerId,
                item.Confidence, $"Distribution recovery: safe current-frame role evidence fills available survivor slot {emptySlot}; visual slot {item.Slot.Index} is not treated as the internal Pick slot.",
                DependencyGroup: recoveryGroup, RequireEmptySurvivorSlot: true));
            simulated[emptySlot] = item.Character.ResolvedCharacterName;
            messages.Add($"Distribution recovery planned: fill available Sur[{emptySlot}] with {item.Character.ResolvedCharacterName} from visual slot {item.Slot.Index}.");
        }

        var assignments = new List<(SmartBpRecognizedPlayerCharacterSlot Slot, int Target, SmartBpNormalizedCharacter Character, string DisplayName, double Confidence)>();
        foreach (var item in uniqueRoles)
        {
            if (string.IsNullOrWhiteSpace(item.Slot.PlayerId))
            {
                messages.Add($"Distribution assignment skipped for visual slot {item.Slot.Index}: player_id missing; role recovery remains allowed.");
                continue;
            }
            var playerMatch = matcher.MatchSurvivorPlayer(item.Slot.PlayerId);
            if (!playerMatch.IsMatched || !playerMatch.IsSafe)
            {
                messages.Add($"Distribution assignment skipped for visual slot {item.Slot.Index}: player_id '{item.Slot.PlayerId}' did not match safely ({playerMatch.Reason}).");
                continue;
            }
            assignments.Add((item.Slot, playerMatch.Index, item.Character,
                playerMatch.DisplayName ?? item.Slot.PlayerId, item.Confidence));
            messages.Add($"Player identity matched: {item.Slot.PlayerId} -> internal Sur[{playerMatch.Index}] ({playerMatch.DisplayName}), score={playerMatch.Score:0.00}.");
        }

        var conflictedTargets = assignments
            .GroupBy(item => item.Target)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        foreach (var item in assignments.Where(item => !conflictedTargets.Contains(item.Target)))
        {
            var slot = item.Slot;
            var target = item.Target;
            var resolved = item.Character;

            var source = Array.FindIndex(simulated, x => x == resolved.ResolvedCharacterName);
            if (source < 0)
            {
                messages.Add($"Skipped distribution for player {item.DisplayName}: character {resolved.ResolvedCharacterName} is not among currently selected survivors; distribution cannot introduce new characters.");
                continue;
            }
            if (source == target)
            {
                messages.Add($"Skipped distribution no-op: player {item.DisplayName} already has {resolved.ResolvedCharacterName}.");
                continue;
            }

            messages.Add($"Distribution operation: swap existing {resolved.ResolvedCharacterName} source={source} target={target}.");
            operations.Add(new(SmartBpDetectedOperationKind.SwapSurvivors, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, target, slot.CharacterName,
                resolved.ResolvedCharacterName, slot.PlayerId,
                item.Confidence, $"Distribution: place existing character {resolved.ResolvedCharacterName} onto player {item.DisplayName} internal slot {target}.",
                DependencyGroup: recoveryGroup));
            (simulated[source], simulated[target]) = (simulated[target], simulated[source]);
        }
        return new(operations, messages);
    }

    private IReadOnlyList<SmartBpDetectedOperation> BuildDistribution(
        SmartBpFocusedExtractionResult extraction,
        IReadOnlyList<int> guidanceIndexes)
    {
        var slots = extraction.Slots
            .Where(x => x.CharacterName != null && !SmartBpBusinessStateParser.IsUnselected(x.CharacterName) && x.SlotIndex is >= 0 and < 4)
            .Select(x => new SmartBpRecognizedPlayerCharacterSlot
            {
                Index = x.SlotIndex,
                CharacterName = x.CharacterName!,
                PlayerId = x.PlayerId,
                SlotState = SmartBpRecognizedSlotState.Selected,
                RecognitionConfidence = x.Confidence,
                IsAutoApplySafe = x.Confidence >= .90
            });
        return BuildDistribution(slots, guidanceIndexes).Operations;
    }
}
