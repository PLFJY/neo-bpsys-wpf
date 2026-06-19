using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal static class SmartBpAutomaticMapping
{
    public static (string Region, string Camp, string Meaning) Get(GameAction action) => action switch
    {
        GameAction.BanSur => ("right_top", "survivor", "the hunter-side operation area for banning survivors"),
        GameAction.BanHun => ("left_top", "hunter", "the survivor-side operation area for banning hunters"),
        GameAction.PickSur => ("left_bottom", "survivor", "the survivor picking area"),
        GameAction.DistributeChara => ("left_bottom", "survivor", "fixed survivor player slots with assigned characters"),
        GameAction.PickHun => ("right_bottom", "hunter", "the hunter picking area"),
        _ => throw new NotSupportedException($"GameGuidance action {action} is not supported by BP recognition.")
    };

    public static SmartBpRecognitionTask ToRecognitionTask(GameAction action) => action switch
    {
        GameAction.BanSur => SmartBpRecognitionTask.BanSur,
        GameAction.BanHun => SmartBpRecognitionTask.BanHun,
        GameAction.PickSur => SmartBpRecognitionTask.PickSur,
        GameAction.DistributeChara => SmartBpRecognitionTask.CharacterDistribution,
        GameAction.PickHun => SmartBpRecognitionTask.PickHun,
        _ => throw new NotSupportedException($"GameGuidance action {action} is not supported by BP recognition.")
    };

    public static bool TryParseDetectedAction(string value, out GameAction action)
    {
        action = value switch
        {
            "BanSur" => GameAction.BanSur,
            "BanHun" => GameAction.BanHun,
            "PickSur" => GameAction.PickSur,
            "DistributeChara" => GameAction.DistributeChara,
            "PickHun" => GameAction.PickHun,
            _ => GameAction.None
        };
        return action != GameAction.None;
    }
}

internal static class SmartBpAutomaticParser
{
    public static SmartBpStageDetectionResult ParseStage(string raw)
    {
        var result = JsonSerializer.Deserialize<SmartBpStageDetectionResult>(raw)
            ?? throw new InvalidDataException("Stage detection JSON is empty.");
        result.Evidence ??= [];
        result.Warnings ??= [];
        if (result.SchemaVersion != 1) throw new InvalidDataException("Unsupported stage schema_version.");
        if (result.RecognizedAction is not ("BanSur" or "BanHun" or "PickSur" or "DistributeChara" or "PickHun" or "Unknown"))
            throw new InvalidDataException("Invalid recognized_action.");
        if (result.ActiveSide is not ("left" or "right" or "unknown")) throw new InvalidDataException("Invalid active_side.");
        if (result.OperationRegion is not ("left_top" or "left_bottom" or "right_top" or "right_bottom" or "unknown"))
            throw new InvalidDataException("Invalid operation_region.");
        if (result.OperationOwner is not ("survivor" or "hunter" or "unknown")) throw new InvalidDataException("Invalid operation_owner.");
        if (result.TargetCamp is not ("survivor" or "hunter" or "unknown")) throw new InvalidDataException("Invalid target_camp.");
        if (result.Confidence is < 0 or > 1) throw new InvalidDataException("Invalid stage confidence.");
        if (SmartBpAutomaticMapping.TryParseDetectedAction(result.RecognizedAction, out var action))
        {
            var expected = SmartBpAutomaticMapping.Get(action);
            if (result.OperationRegion != expected.Region || result.TargetCamp != expected.Camp)
                throw new InvalidDataException("Detected stage conflicts with the BP region mapping.");
        }
        return result;
    }

    public static SmartBpFocusedExtractionResult ParseFocused(string raw, GameAction expectedAction)
    {
        var result = JsonSerializer.Deserialize<SmartBpFocusedExtractionResult>(raw)
            ?? throw new InvalidDataException("Focused extraction JSON is empty.");
        result.Slots ??= [];
        result.Warnings ??= [];
        var expected = SmartBpAutomaticMapping.Get(expectedAction);
        if (result.SchemaVersion != 1) throw new InvalidDataException("Unsupported focused schema_version.");
        if (result.Task != expectedAction.ToString()) throw new InvalidDataException("Unexpected focused task.");
        if (result.OperationRegion != expected.Region || result.TargetCamp != expected.Camp)
            throw new InvalidDataException("Focused extraction conflicts with the current guidance step.");
        foreach (var slot in result.Slots)
        {
            if (slot.SlotIndex is < -1 or > 15) throw new InvalidDataException("Invalid focused slot index.");
            if (slot.Confidence is < 0 or > 1) throw new InvalidDataException("Invalid focused confidence.");
            if (slot.SlotState is not ("selected" or "waiting" or "unselected" or "banned" or "unknown"))
                throw new InvalidDataException("Invalid focused slot_state.");
        }
        return result;
    }
}

internal sealed class SmartBpGuidanceSyncService(
    IGameGuidanceService guidance,
    ISmartBpRecognitionSettingsService settings) : ISmartBpGuidanceSyncService
{
    public async Task<SmartBpGuidanceSyncResult> SyncAsync(SmartBpStageDetectionResult detectedStage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (detectedStage.Confidence < settings.Settings.StageConfidenceThreshold)
            return Reject($"Stage confidence {detectedStage.Confidence:0.00} is below {settings.Settings.StageConfidenceThreshold:0.00}.");
        if (!SmartBpAutomaticMapping.TryParseDetectedAction(detectedStage.RecognizedAction, out var action))
            return Reject("The detected BP action is unknown.");

        var snapshot = guidance.GetRuntimeSnapshot();
        if (!snapshot.IsStarted)
        {
            var error = await guidance.StartGuidance();
            if (!string.IsNullOrWhiteSpace(error)) return Reject(error);
            snapshot = guidance.GetRuntimeSnapshot();
        }
        if (!snapshot.IsStarted || snapshot.Workflow.Count == 0) return Reject("GameGuidance is not available.");

        GameGuidanceStepSnapshot? target = null;
        if (snapshot.CurrentAction == action && snapshot.CurrentStepIndex >= 0)
            target = snapshot.Workflow.FirstOrDefault(x => x.StepIndex == snapshot.CurrentStepIndex);
        if (target == null)
        {
            var last = Math.Min(snapshot.Workflow.Count - 1,
                Math.Max(snapshot.CurrentStepIndex, -1) + settings.Settings.GuidanceSyncLookAheadSteps);
            target = snapshot.Workflow
                .Where(x => x.StepIndex > snapshot.CurrentStepIndex && x.StepIndex <= last && x.Action == action)
                .OrderBy(x => x.StepIndex)
                .FirstOrDefault();
        }
        if (target == null) return Reject($"No forward {action} step exists within the configured lookahead window.", action);
        if (target.StepIndex == snapshot.CurrentStepIndex)
            return new(false, true, "Current GameGuidance step already matches the detected stage.", target.Action, target.Indexes, target.StepIndex);

        cancellationToken.ThrowIfCancellationRequested();
        var moveError = await guidance.MoveToStepAsync(target.StepIndex);
        if (!string.IsNullOrWhiteSpace(moveError)) return Reject(moveError, action);
        return new(true, true, $"GameGuidance moved forward to step {target.StepIndex}.", target.Action, target.Indexes, target.StepIndex);
    }

    private static SmartBpGuidanceSyncResult Reject(string reason, GameAction? action = null) =>
        new(false, false, reason, action, [], null);
}

internal sealed class SmartBpCandidateOperationBuilder(ISmartBpCharacterResolver resolver, ISharedDataService shared)
{
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
            if (slot.CharacterName == null) continue;
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
                slot.CharacterName, resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName,
                slot.PlayerId, slot.Confidence, reason));
        }
        return operations;
    }

    private IReadOnlyList<SmartBpDetectedOperation> BuildDistribution(
        SmartBpFocusedExtractionResult extraction,
        IReadOnlyList<int> guidanceIndexes)
    {
        var operations = new List<SmartBpDetectedOperation>();
        var simulated = shared.CurrentGame.SurPlayerList.Select(x => x.Character?.Name).ToArray();
        foreach (var slot in extraction.Slots.Where(x => x.CharacterName != null && x.SlotIndex is >= 0 and < 4).OrderBy(x => x.SlotIndex))
        {
            var resolved = resolver.Resolve(slot.CharacterName, Camp.Sur, slot.SlotIndex, slot.Confidence);
            if (resolved.ResolvedCharacterName != null && simulated[slot.SlotIndex] == resolved.ResolvedCharacterName) continue;
            operations.Add(new(SmartBpDetectedOperationKind.SwapSurvivors, GameAction.DistributeChara,
                guidanceIndexes.ToArray(), Camp.Sur, slot.SlotIndex, slot.CharacterName,
                resolved.ResolvedCharacterKey, resolved.ResolvedCharacterName, slot.PlayerId,
                slot.Confidence, $"Place the detected character into fixed survivor player slot {slot.SlotIndex}."));
            if (resolved.ResolvedCharacterName == null) continue;
            var source = Array.FindIndex(simulated, x => x == resolved.ResolvedCharacterName);
            if (source < 0) continue;
            (simulated[source], simulated[slot.SlotIndex]) = (simulated[slot.SlotIndex], simulated[source]);
        }
        return operations;
    }
}

internal sealed class SmartBpDetectedOperationApplier(
    ICharacterSelectionService selection,
    IGameGuidanceService guidance,
    ISharedDataService shared) : ISmartBpDetectedOperationApplier
{
    public async Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var applied = 0;
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = guidance.GetRuntimeSnapshot();
            if (snapshot.CurrentAction != operation.SourceGuidanceAction) { warnings.Add("Skipped an operation because GameGuidance moved."); continue; }
            if (!snapshot.CurrentIndexes.SequenceEqual(operation.SourceGuidanceIndexes)) { warnings.Add("Skipped an operation because the GameGuidance indexes changed."); continue; }
            if (operation.Confidence < 0.90) { warnings.Add($"Skipped low-confidence character {operation.RawCharacterName}."); continue; }
            if (operation.ResolvedCharacterKey == null) { warnings.Add($"Skipped unresolved character {operation.RawCharacterName}."); continue; }
            var dictionary = operation.Camp == Camp.Sur ? shared.SurCharaDict : shared.HunCharaDict;
            if (!dictionary.TryGetValue(operation.ResolvedCharacterKey, out var character)) { warnings.Add($"Resolved character key no longer exists: {operation.ResolvedCharacterKey}."); continue; }

            switch (operation.Kind)
            {
                case SmartBpDetectedOperationKind.BanCharacter:
                    await selection.BanCharacterAsync(operation.Camp, operation.SlotIndex, character);
                    break;
                case SmartBpDetectedOperationKind.PickSurvivor:
                    await selection.SelectSurvivorAsync(operation.SlotIndex, character);
                    break;
                case SmartBpDetectedOperationKind.PickHunter:
                    await selection.SelectHunterAsync(character);
                    break;
                case SmartBpDetectedOperationKind.SwapSurvivors:
                    var sourceMatch = shared.CurrentGame.SurPlayerList
                        .Select((player, index) => (player, index))
                        .FirstOrDefault(x => ReferenceEquals(x.player.Character, character) || x.player.Character?.Name == character.Name);
                    if (sourceMatch.player == null) { warnings.Add($"Cannot swap {character.Name} because it is not present in the current survivor order."); continue; }
                    var source = sourceMatch.index;
                    if (source != operation.SlotIndex) await selection.SwapSurvivorsAsync(source, operation.SlotIndex);
                    break;
            }
            applied++;
        }
        return new(applied, warnings);
    }
}

internal sealed class SmartBpAutoRecognitionCoordinator(
    ISmartBpImageEncoder encoder,
    ILlamaCppOpenAiClient client,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpGuidanceSyncService guidanceSync,
    IGameGuidanceService guidance,
    SmartBpCandidateOperationBuilder candidateBuilder,
    ISmartBpDetectedOperationApplier applier) : ISmartBpAutoRecognitionCoordinator
{
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private CancellationTokenSource? _runCancellation;
    public bool IsRunning => _runCancellation is { IsCancellationRequested: false };

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        _runCancellation = null;
        return Task.CompletedTask;
    }

    public async Task<SmartBpAutoRecognitionTickResult> RunOneTickAsync(BitmapSource frame, CancellationToken cancellationToken = default)
    {
        if (!await _tickGate.WaitAsync(0, cancellationToken))
            return Failure("An automatic recognition tick is already running.");
        string rawStage = "", rawFocused = "";
        try
        {
            var image = await Task.Run(() => encoder.EncodeDataUrl(frame, settings.Settings.MaxImageWidth), cancellationToken);
            rawStage = await client.DetectStageAsync(image, cancellationToken);
            var stage = await Task.Run(() => SmartBpAutomaticParser.ParseStage(rawStage), cancellationToken);
            SmartBpGuidanceSyncResult? sync = settings.Settings.EnableAutoGuidanceSync
                ? await guidanceSync.SyncAsync(stage, cancellationToken)
                : new(false, false, "Automatic GameGuidance synchronization is disabled.", null, [], null);
            var snapshot = guidance.GetRuntimeSnapshot();
            if (!snapshot.IsStarted || snapshot.CurrentAction is not { } action)
                return new(stage, sync, snapshot, null, [], rawStage, "", "GameGuidance is not started; focused extraction was skipped.");
            if (stage.Confidence < settings.Settings.StageConfidenceThreshold)
                return new(stage, sync, snapshot, null, [], rawStage, "", "Stage confidence is below the automatic recognition threshold.");
            if (!SmartBpAutomaticMapping.TryParseDetectedAction(stage.RecognizedAction, out var detectedAction) || detectedAction != action)
                return new(stage, sync, snapshot, null, [], rawStage, "", $"Detected stage {stage.RecognizedAction} does not match current GameGuidance action {action}.");
            try { _ = SmartBpAutomaticMapping.Get(action); }
            catch (NotSupportedException)
            {
                return new(stage, sync, snapshot, null, [], rawStage, "", $"Current GameGuidance action {action} is not a BP character operation.");
            }
            rawFocused = await client.RecognizeFocusedAsync(image, action, snapshot.CurrentIndexes, cancellationToken);
            var focused = await Task.Run(() => SmartBpAutomaticParser.ParseFocused(rawFocused, action), cancellationToken);
            var operations = candidateBuilder.Build(focused, action, snapshot.CurrentIndexes);
            if (settings.Settings.EnableAutoApplyRecognition)
                await applier.ApplyAsync(operations, cancellationToken);
            return new(stage, sync, snapshot, focused, operations, rawStage, rawFocused, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex is LlamaCppRequestException request && string.IsNullOrEmpty(rawFocused)) rawFocused = request.RawResponse;
            return new(null, null, guidance.GetRuntimeSnapshot(), null, [], rawStage, rawFocused, ex.Message);
        }
        finally { _tickGate.Release(); }
    }

    private SmartBpAutoRecognitionTickResult Failure(string error) =>
        new(null, null, guidance.GetRuntimeSnapshot(), null, [], "", "", error);
}
