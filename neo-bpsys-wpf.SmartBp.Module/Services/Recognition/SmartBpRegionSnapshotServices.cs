using System.ComponentModel;
using System.Text;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class SmartBpBusinessStateMerger : ISmartBpBusinessStateMerger
{
    public SmartBpBusinessStateRecognitionResult Merge(
        SmartBpPhaseRecognitionResult phase,
        SmartBpFocusedBusinessExtractionResult? bannedSur,
        SmartBpFocusedBusinessExtractionResult? bannedHun,
        SmartBpFocusedBusinessExtractionResult? pickedSur,
        SmartBpFocusedBusinessExtractionResult? pickedHun)
    {
        var result = new SmartBpBusinessStateRecognitionResult
        {
            Phase = phase.Phase,
            BannedSur = bannedSur?.Slots.Select(ToCharacterSlot).ToList() ?? DefaultCharacterSlots(4),
            BannedHun = bannedHun?.Slots.Select(ToCharacterSlot).ToList() ?? DefaultCharacterSlots(2),
            PickedSur = pickedSur?.Slots.Select(ClonePlayerSlot).ToList() ?? DefaultPlayerSlots(4),
            PickedHun = pickedHun?.PickedHun is { } hunter
                ? ClonePlayerSlot(hunter)
                : new SmartBpRecognizedPlayerCharacterSlot { Index = 0, CharacterName = "未选择" }
        };
        SmartBpBusinessStateParser.NormalizeAndValidate(result);
        return result;
    }

    private static List<SmartBpRecognizedCharacterSlot> DefaultCharacterSlots(int count) =>
        Enumerable.Range(0, count).Select(index => new SmartBpRecognizedCharacterSlot { Index = index, CharacterName = "未选择" }).ToList();

    private static List<SmartBpRecognizedPlayerCharacterSlot> DefaultPlayerSlots(int count) =>
        Enumerable.Range(0, count).Select(index => new SmartBpRecognizedPlayerCharacterSlot { Index = index, CharacterName = "未选择" }).ToList();

    private static SmartBpRecognizedCharacterSlot ToCharacterSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName };

    private static SmartBpRecognizedPlayerCharacterSlot ClonePlayerSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, PlayerId = slot.PlayerId };
}

internal sealed class SmartBpRegionSnapshotRecognitionService(
    ISmartBpImageEncoder encoder,
    ISmartBpRecognitionFrameCropper cropper,
    ILlamaCppOpenAiClient client,
    ISmartBpRecognitionSettingsService settings,
    ISharedDataService shared,
    ISmartBpBusinessStateMerger merger) : ISmartBpRegionSnapshotRecognitionService
{
    public async Task<SmartBpRegionSnapshot> RecognizeSnapshotAsync(
        BitmapSource frame,
        SmartBpRegionSnapshotRecognitionMode mode,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        if (mode == SmartBpRegionSnapshotRecognitionMode.PendingAndCurrentRegions)
            diagnostics.Add("PendingAndCurrentRegions currently uses all four content regions for correctness.");

        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        var phaseCrop = await CropAsync(frame, SmartBpRecognitionRegion.PhaseTop, cancellationToken);
        var phaseRaw = await client.RecognizePhaseAsync(await EncodeAsync(phaseCrop.Image, cancellationToken), cancellationToken);
        raw["phase_top"] = phaseRaw;
        var phase = SmartBpAutomaticParser.ParsePhase(phaseRaw);
        diagnostics.Add($"Phase crop: {phaseCrop.PixelRectText}; detected={phase.Phase}.");

        var contentCrops = new List<SmartBpCroppedFrame>(4);
        var bannedHun = await RecognizeRegionAsync(frame, SmartBpRecognitionRegion.LeftTop, GameAction.BanHun, "left_top", raw, contentCrops, diagnostics, cancellationToken);
        var bannedSur = await RecognizeRegionAsync(frame, SmartBpRecognitionRegion.RightTop, GameAction.BanSur, "right_top", raw, contentCrops, diagnostics, cancellationToken);
        var pickedSur = await RecognizeRegionAsync(frame, SmartBpRecognitionRegion.LeftBottom, GameAction.PickSur, "left_bottom", raw, contentCrops, diagnostics, cancellationToken);
        var pickedHun = await RecognizeRegionAsync(frame, SmartBpRecognitionRegion.RightBottom, GameAction.PickHun, "right_bottom", raw, contentCrops, diagnostics, cancellationToken);
        var businessState = merger.Merge(phase, bannedSur, bannedHun, pickedSur, pickedHun);

        return new SmartBpRegionSnapshot
        {
            Phase = phase,
            BannedSurRegion = bannedSur,
            BannedHunRegion = bannedHun,
            PickedSurRegion = pickedSur,
            PickedHunRegion = pickedHun,
            BusinessState = businessState,
            Diagnostics = diagnostics,
            PhaseCrop = phaseCrop,
            ContentCrops = contentCrops,
            RawResponses = raw
        };
    }

    private async Task<SmartBpFocusedBusinessExtractionResult> RecognizeRegionAsync(
        BitmapSource frame,
        SmartBpRecognitionRegion region,
        GameAction semanticAction,
        string rawKey,
        IDictionary<string, string> raw,
        ICollection<SmartBpCroppedFrame> crops,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var crop = await CropAsync(frame, region, cancellationToken);
        crops.Add(crop);
        var response = await client.RecognizeFocusedBusinessAsync(await EncodeAsync(crop.Image, cancellationToken), semanticAction, cancellationToken);
        raw[rawKey] = response;
        var parsed = SmartBpAutomaticParser.ParseFocusedBusiness(
            response,
            semanticAction,
            shared.SurCharaDict.Keys.ToArray(),
            shared.HunCharaDict.Keys.ToArray());
        diagnostics.Add($"Content crop: {region}, {crop.PixelRectText}; target={parsed.TargetField}.");
        return parsed;
    }

    private Task<SmartBpCroppedFrame> CropAsync(BitmapSource frame, SmartBpRecognitionRegion region, CancellationToken cancellationToken) =>
        Task.Run(() => cropper.CropWithInfo(frame, region), cancellationToken);

    private Task<string> EncodeAsync(BitmapSource image, CancellationToken cancellationToken) =>
        Task.Run(() => encoder.EncodeDataUrl(image, settings.Settings.MaxImageWidth), cancellationToken);
}

internal sealed class SmartBpRecognitionLedger : ISmartBpRecognitionLedger, IDisposable
{
    private readonly object _gate = new();
    private readonly HashSet<SmartBpWorkflowOperationKey> _completed = [];
    private readonly Dictionary<SmartBpWorkflowOperationKey, string> _skipped = [];
    private readonly ISharedDataService _shared;
    private Game? _observedGame;

    public SmartBpRecognitionLedger(ISharedDataService shared)
    {
        _shared = shared;
        _shared.CurrentGameChanged += OnCurrentGameChanged;
        ObserveCurrentGame();
    }

    public bool IsStepOperationCompleted(SmartBpWorkflowOperationKey key)
    {
        lock (_gate) return _completed.Contains(key);
    }

    public void MarkCompleted(SmartBpWorkflowOperationKey key)
    {
        lock (_gate)
        {
            _completed.Add(key);
            _skipped.Remove(key);
        }
    }

    public void MarkSkipped(SmartBpWorkflowOperationKey key, string reason)
    {
        lock (_gate) _skipped[key] = reason;
    }

    public void ResetForCurrentGame()
    {
        lock (_gate)
        {
            _completed.Clear();
            _skipped.Clear();
        }
    }

    public void Dispose()
    {
        _shared.CurrentGameChanged -= OnCurrentGameChanged;
        if (_observedGame != null) _observedGame.PropertyChanged -= OnGamePropertyChanged;
        ResetForCurrentGame();
    }

    private void OnCurrentGameChanged(object? sender, EventArgs e)
    {
        ObserveCurrentGame();
        ResetForCurrentGame();
    }

    private void ObserveCurrentGame()
    {
        if (_observedGame != null) _observedGame.PropertyChanged -= OnGamePropertyChanged;
        _observedGame = _shared.CurrentGame;
        if (_observedGame != null) _observedGame.PropertyChanged += OnGamePropertyChanged;
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game.GameProgress)) ResetForCurrentGame();
    }
}

internal sealed class SmartBpWorkflowBackfillService(
    SmartBpCandidateOperationBuilder candidateBuilder,
    ISmartBpRecognitionLedger ledger,
    ISharedDataService shared) : ISmartBpWorkflowBackfillService
{
    public SmartBpWorkflowBackfillPlan BuildPlan(
        SmartBpBusinessStateRecognitionResult snapshot,
        GameGuidanceRuntimeSnapshot guidanceSnapshot)
    {
        if (!guidanceSnapshot.IsStarted || guidanceSnapshot.Workflow.Count == 0 || guidanceSnapshot.CurrentStepIndex < 0)
            return new([], ["GameGuidance is not started; merged snapshot is available for preview only."]);

        var sets = new List<SmartBpWorkflowStepCandidateSet>();
        var diagnostics = new List<string>();
        foreach (var step in guidanceSnapshot.Workflow.OrderBy(item => item.StepIndex).Where(item => item.StepIndex <= guidanceSnapshot.CurrentStepIndex))
        {
            if (!SmartBpAutomaticMapping.IsCharacterOperationAction(step.Action))
            {
                if (step.StepIndex == guidanceSnapshot.CurrentStepIndex)
                    sets.Add(new(step.StepIndex, step.Action, step.Indexes, [], "Current workflow step has no character operation."));
                continue;
            }

            var built = candidateBuilder.BuildWithDiagnostics(snapshot, step.Action, step.Indexes);
            diagnostics.AddRange(built.Messages.Select(message => $"Step {step.StepIndex} {step.Action}: {message}"));
            var mode = step.StepIndex == guidanceSnapshot.CurrentStepIndex && guidanceSnapshot.CurrentAction == step.Action
                ? SmartBpDetectedOperationApplyMode.CurrentStep
                : SmartBpDetectedOperationApplyMode.Backfill;
            var operations = new List<SmartBpDetectedOperation>();
            foreach (var operation in built.Operations)
            {
                var enriched = operation with { SourceWorkflowStepIndex = step.StepIndex, ApplyMode = mode };
                var key = CreateKey(shared.CurrentGame.GameProgress, enriched);
                if (key != null && ledger.IsStepOperationCompleted(key))
                {
                    diagnostics.Add($"Step {step.StepIndex} {step.Action}: skipped ledger-completed {operation.Camp}[{operation.SlotIndex}] {operation.RawCharacterName}.");
                    continue;
                }
                operations.Add(enriched);
            }
            sets.Add(new(step.StepIndex, step.Action, step.Indexes, operations,
                mode == SmartBpDetectedOperationApplyMode.Backfill ? "Previous unresolved character step; backfill before current step." : "Current workflow step."));
        }
        return new(sets, diagnostics);
    }

    internal static SmartBpWorkflowOperationKey? CreateKey(GameProgress progress, SmartBpDetectedOperation operation) =>
        operation.SourceWorkflowStepIndex is not { } stepIndex
            ? null
            : new(progress, stepIndex, operation.SourceGuidanceAction, operation.SlotIndex, operation.Camp, operation.ResolvedCharacterKey);
}
