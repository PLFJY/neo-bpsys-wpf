using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 仅在角色 BP 阶段切换时回看短暂的历史帧，补回刚结束步骤遗漏的高置信 Ban/Pick。
/// </summary>
internal sealed class SmartBpTransitionReplayService(
    ISmartBpFrameRingBuffer frameBuffer,
    ISmartBpOcrBpRecognitionService ocr,
    ISmartBpRecognitionSettingsService settings,
    SmartBpCandidateOperationBuilder candidates,
    ISharedDataService shared) : ISmartBpTransitionReplayService
{
    /// <inheritdoc />
    public async Task<SmartBpTransitionReplayResult?> BuildAsync(
        GameGuidanceRuntimeSnapshot sourceGuidance,
        GameAction targetAction,
        long currentFrameSequence,
        CancellationToken cancellationToken = default)
    {
        if (sourceGuidance.CurrentAction is not { } sourceAction ||
            sourceAction is not (GameAction.BanSur or GameAction.BanHun or GameAction.PickSur or GameAction.PickHun) ||
            sourceAction == targetAction)
            return null;

        var region = GetRegion(sourceAction);
        var field = GetField(sourceAction);
        var diagnostics = new List<string>
        {
            $"Transition replay triggered: source=step {sourceGuidance.CurrentStepIndex} {sourceAction}, target={targetAction}, field={field}."
        };
        var frames = frameBuffer.GetRecentFrames(TimeSpan.FromMilliseconds(settings.Settings.RecognitionTransitionLookBehindMilliseconds))
            .Where(item => item.Sequence < currentFrameSequence)
            .OrderBy(item => item.Sequence)
            .ToArray();
        if (frames.Length == 0)
        {
            diagnostics.Add("Transition replay skipped: no buffered historical frame is available in the configured window.");
            return new(sourceGuidance.CurrentStepIndex, sourceAction, targetAction, [], [], diagnostics);
        }

        var latestBySlot = new Dictionary<(SmartBpDetectedOperationKind Kind, Camp Camp, int Slot), SmartBpDetectedOperation>();
        var frameResults = new List<SmartBpTransitionReplayFrameResult>();
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frameDiagnostics = new List<string>();
            var result = await ocr.RecognizeAsync(frame.Frame,
                new SmartBpOcrRecognitionRequest([region], IncludePhase: false,
                    ParseContext: new SmartBpOcrFieldParseContext
                    {
                        AuthoritativePhase = GetPhase(sourceAction),
                        CurrentGuidanceAction = sourceAction,
                        SurvivorPickLocked = false,
                        IsAutomaticMode = true
                    }), cancellationToken).ConfigureAwait(false);
            frameDiagnostics.AddRange(result.Diagnostics);
            var built = candidates.BuildWithDiagnostics(result.BusinessState, sourceAction, sourceGuidance.CurrentIndexes);
            frameDiagnostics.AddRange(built.Messages);
            var accepted = 0;
            foreach (var operation in built.Operations)
            {
                if (operation.Confidence < settings.Settings.RecognitionTransitionReplayMinimumConfidence)
                {
                    frameDiagnostics.Add($"Skipped replay {Describe(operation)}: confidence {operation.Confidence:0.00} is below configured threshold.");
                    continue;
                }
                if (operation.ResolvedCharacterKey == null || operation.ResolvedCharacterName == null)
                {
                    frameDiagnostics.Add($"Skipped replay {Describe(operation)}: character could not be resolved safely.");
                    continue;
                }
                if (!IsDifferentFromCurrentGame(operation))
                {
                    frameDiagnostics.Add($"Skipped replay {Describe(operation)}: local game already has the same character.");
                    continue;
                }
                latestBySlot[(operation.Kind, operation.Camp, operation.SlotIndex)] = operation with
                {
                    SourceWorkflowStepIndex = sourceGuidance.CurrentStepIndex,
                    ApplyMode = SmartBpDetectedOperationApplyMode.Backfill,
                    Reason = $"Transition replay frame {frame.Sequence}: {operation.Reason}"
                };
                accepted++;
            }
            frameResults.Add(new(frame.Sequence, frame.Timestamp, field, accepted, frameDiagnostics));
        }

        var operations = latestBySlot.Values.ToArray();
        diagnostics.Add($"Transition replay completed: frames={frames.Length}, accepted_operations={operations.Length}, minimum_confidence={settings.Settings.RecognitionTransitionReplayMinimumConfidence:0.00}.");
        return new(sourceGuidance.CurrentStepIndex, sourceAction, targetAction, frameResults, operations, diagnostics);
    }

    private bool IsDifferentFromCurrentGame(SmartBpDetectedOperation operation)
    {
        var game = shared.CurrentGame;
        return operation.Kind switch
        {
            SmartBpDetectedOperationKind.BanCharacter when operation.Camp == Camp.Sur && operation.SlotIndex >= 0 && operation.SlotIndex < game.CurrentSurBannedList.Count =>
                !string.Equals(game.CurrentSurBannedList[operation.SlotIndex]?.Name, operation.ResolvedCharacterName, StringComparison.Ordinal),
            SmartBpDetectedOperationKind.BanCharacter when operation.Camp == Camp.Hun && operation.SlotIndex >= 0 && operation.SlotIndex < game.CurrentHunBannedList.Count =>
                !string.Equals(game.CurrentHunBannedList[operation.SlotIndex]?.Name, operation.ResolvedCharacterName, StringComparison.Ordinal),
            SmartBpDetectedOperationKind.PickSurvivor when operation.SlotIndex is >= 0 and < 4 =>
                !string.Equals(game.SurPlayerList[operation.SlotIndex].Character?.Name, operation.ResolvedCharacterName, StringComparison.Ordinal),
            SmartBpDetectedOperationKind.PickHunter =>
                !string.Equals(game.HunPlayer.Character?.Name, operation.ResolvedCharacterName, StringComparison.Ordinal),
            _ => false
        };
    }

    private static SmartBpRecognitionRegion GetRegion(GameAction action) => action switch
    {
        GameAction.BanSur => SmartBpRecognitionRegion.RightTop,
        GameAction.BanHun => SmartBpRecognitionRegion.LeftTop,
        GameAction.PickSur => SmartBpRecognitionRegion.LeftBottom,
        GameAction.PickHun => SmartBpRecognitionRegion.RightBottom,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static string GetField(GameAction action) => action switch
    {
        GameAction.BanSur => "banned_sur",
        GameAction.BanHun => "banned_hun",
        GameAction.PickSur => "picked_sur",
        GameAction.PickHun => "picked_hun",
        _ => "unknown"
    };

    private static string GetPhase(GameAction action) => action switch
    {
        GameAction.BanSur => "屏蔽求生者",
        GameAction.BanHun => "屏蔽监管者",
        GameAction.PickSur => "选择求生者",
        GameAction.PickHun => "选择监管者",
        _ => "未知"
    };

    private static string Describe(SmartBpDetectedOperation operation) =>
        $"{operation.Kind} {operation.Camp}[{operation.SlotIndex}] {operation.RawCharacterName ?? "<empty>"}";
}
