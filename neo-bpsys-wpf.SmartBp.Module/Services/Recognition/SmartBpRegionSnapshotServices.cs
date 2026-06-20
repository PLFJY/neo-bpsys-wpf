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
        new() { Index = slot.Index, CharacterName = slot.CharacterName, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason };

    private static SmartBpRecognizedPlayerCharacterSlot ClonePlayerSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, PlayerId = slot.PlayerId, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason };
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

internal sealed class SmartBpRecognitionStateStore : ISmartBpRecognitionStateStore
{
    private readonly object _gate = new();
    private SmartBpRecognitionState _state = new();

    public SmartBpBusinessStateRecognitionResult Snapshot
    {
        get
        {
            lock (_gate) return ToSnapshot(_state);
        }
    }

    public IReadOnlyList<string> ApplyDelta(SmartBpSnapshotDeltaResult delta, long frameSequence, DateTimeOffset timestamp)
    {
        var diagnostics = new List<string>();
        lock (_gate)
        {
            _state.Phase = delta.Phase;
            _state.LastFrameSequence = Math.Max(_state.LastFrameSequence, frameSequence);
            foreach (var update in delta.Updates)
            {
                if (_state.FieldFrameSequences.TryGetValue(update.Field, out var existing) && frameSequence < existing)
                {
                    diagnostics.Add($"Ignored stale field update {update.Field} from frame sequence {frameSequence}; latest={existing}.");
                    continue;
                }
                switch (update.Field)
                {
                    case "banned_sur":
                        _state.BannedSur = update.Slots?.Select(ToCharacterSlot).ToList() ?? _state.BannedSur;
                        break;
                    case "banned_hun":
                        _state.BannedHun = update.Slots?.Select(ToCharacterSlot).ToList() ?? _state.BannedHun;
                        break;
                    case "picked_sur":
                        _state.PickedSur = update.Slots?.Select(ClonePlayerSlot).ToList() ?? _state.PickedSur;
                        break;
                    case "picked_hun":
                        if (update.PickedHun != null) _state.PickedHun = ClonePlayerSlot(update.PickedHun);
                        break;
                }
                _state.FieldFrameSequences[update.Field] = frameSequence;
                _state.FieldUpdatedAt[update.Field] = timestamp;
                diagnostics.Add($"Applied delta field {update.Field} from frame sequence {frameSequence}.");
            }
        }
        return diagnostics;
    }

    public IReadOnlyList<string> GetStaleFieldDiagnostics(DateTimeOffset timestamp, int staleMilliseconds)
    {
        if (staleMilliseconds <= 0) return [];
        lock (_gate)
        {
            return new[] { "banned_sur", "banned_hun", "picked_sur", "picked_hun" }
                .Where(field => !_state.FieldUpdatedAt.TryGetValue(field, out var updated) || (timestamp - updated).TotalMilliseconds > staleMilliseconds)
                .Select(field => $"Field {field} is stale or unknown.")
                .ToArray();
        }
    }

    public void Reset()
    {
        lock (_gate) _state = new();
    }

    private static SmartBpBusinessStateRecognitionResult ToSnapshot(SmartBpRecognitionState state)
    {
        var snapshot = new SmartBpBusinessStateRecognitionResult
        {
            Phase = state.Phase,
            BannedSur = state.BannedSur.Select(CloneCharacterSlot).ToList(),
            BannedHun = state.BannedHun.Select(CloneCharacterSlot).ToList(),
            PickedSur = state.PickedSur.Select(ClonePlayerSlot).ToList(),
            PickedHun = ClonePlayerSlot(state.PickedHun)
        };
        SmartBpBusinessStateParser.NormalizeAndValidate(snapshot);
        return snapshot;
    }

    private static SmartBpRecognizedCharacterSlot ToCharacterSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason };

    private static SmartBpRecognizedCharacterSlot CloneCharacterSlot(SmartBpRecognizedCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason };

    private static SmartBpRecognizedPlayerCharacterSlot ClonePlayerSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, PlayerId = slot.PlayerId, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason };
}

internal sealed class SmartBpSnapshotRecognitionPlanner(
    ISmartBpRecognitionSettingsService settings,
    ISmartBpRecognitionStateStore stateStore) : ISmartBpSnapshotRecognitionPlanner
{
    public SmartBpSnapshotDeltaRequest BuildRequest(
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpBusinessStateRecognitionResult currentLocalSnapshot,
        SmartBpRecognitionLedgerSnapshot ledgerSnapshot)
    {
        if (settings.Settings.RecognitionApplyMode == SmartBpRecognitionApplyMode.FreeFullSync)
            return new(
            [
                (SmartBpRecognitionRegion.RightTop, "banned_sur"),
                (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
                (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
                (SmartBpRecognitionRegion.RightBottom, "picked_hun")
            ], ["Free full sync requests every character region."]);
        var requested = new Dictionary<string, SmartBpRecognitionRegion>(StringComparer.Ordinal);
        var diagnostics = new List<string>();
        void Add(string field, SmartBpRecognitionRegion region, string reason)
        {
            if (requested.TryAdd(field, region)) diagnostics.Add($"Request {field} ({region}): {reason}");
        }

        if (guidanceSnapshot.IsStarted && guidanceSnapshot.Workflow.Count > 0)
        {
            var lookBehind = Math.Max(0, settings.Settings.RecognitionEngine == SmartBpRecognitionEngine.Ocr
                ? settings.Settings.OcrBackfillLookBehindSteps
                : settings.Settings.RecognitionBackfillLookBehindSteps);
            var earliest = Math.Max(0, guidanceSnapshot.CurrentStepIndex - lookBehind);
            foreach (var step in guidanceSnapshot.Workflow.OrderBy(x => x.StepIndex)
                         .Where(x => x.StepIndex >= earliest && x.StepIndex <= guidanceSnapshot.CurrentStepIndex))
                AddForAction(step.Action, $"workflow step {step.StepIndex}");
        }

        if (SmartBpAutomaticMapping.TryMapPhase(currentLocalSnapshot.Phase, out var phaseAction))
        {
            if (phaseAction is GameAction.PickSurTalent) Add("picked_sur", SmartBpRecognitionRegion.LeftBottom, "survivor talent phase may need pick backfill");
            else if (phaseAction is GameAction.PickHunTalent) Add("picked_hun", SmartBpRecognitionRegion.RightBottom, "hunter talent phase may need pick backfill");
            else AddForAction(phaseAction, "last detected phase");
        }

        var staleMilliseconds = settings.Settings.RecognitionEngine == SmartBpRecognitionEngine.Ocr
            ? settings.Settings.OcrFieldStaleMilliseconds
            : settings.Settings.RecognitionFieldStaleMilliseconds;
        foreach (var stale in stateStore.GetStaleFieldDiagnostics(DateTimeOffset.Now, staleMilliseconds))
        {
            diagnostics.Add(stale);
            if (stale.Contains("banned_sur", StringComparison.Ordinal)) Add("banned_sur", SmartBpRecognitionRegion.RightTop, "stale field");
            if (stale.Contains("banned_hun", StringComparison.Ordinal)) Add("banned_hun", SmartBpRecognitionRegion.LeftTop, "stale field");
            if (stale.Contains("picked_sur", StringComparison.Ordinal)) Add("picked_sur", SmartBpRecognitionRegion.LeftBottom, "stale field");
            if (stale.Contains("picked_hun", StringComparison.Ordinal)) Add("picked_hun", SmartBpRecognitionRegion.RightBottom, "stale field");
        }

        if (requested.Count == 0)
            diagnostics.Add("Only phase_top is requested this tick.");
        return new(requested.Select(item => (item.Value, item.Key)).ToArray(), diagnostics);

        void AddForAction(GameAction action, string reason)
        {
            if (!SmartBpAutomaticMapping.IsCharacterOperationAction(action)) return;
            var (region, field) = SmartBpAutomaticMapping.GetFocusedTarget(action);
            Add(field, region, reason);
        }
    }
}

internal sealed class SmartBpFrameRingBuffer(ISmartBpRecognitionSettingsService settings) : ISmartBpFrameRingBuffer
{
    private readonly object _gate = new();
    private readonly Queue<SmartBpBufferedFrame> _frames = new();

    public void AddFrame(long sequence, BitmapSource frame, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            _frames.Enqueue(new(sequence, frame, timestamp));
            Trim(timestamp, TimeSpan.FromMilliseconds(settings.Settings.RecognitionFrameBufferMilliseconds));
        }
    }

    public IReadOnlyList<SmartBpBufferedFrame> GetRecentFrames(TimeSpan window)
    {
        var cutoff = DateTimeOffset.Now - window;
        lock (_gate) return _frames.Where(frame => frame.Timestamp >= cutoff).ToArray();
    }

    public SmartBpBufferedFrame? GetBestFrameForRegion(SmartBpRecognitionRegion region, TimeSpan lookBehind)
    {
        return GetRecentFrames(lookBehind).OrderByDescending(frame => frame.Sequence).FirstOrDefault();
    }

    private void Trim(DateTimeOffset now, TimeSpan window)
    {
        while (_frames.TryPeek(out var frame) && now - frame.Timestamp > window)
            _frames.Dequeue();
    }
}

internal sealed class SmartBpCropChangeDetector(ISmartBpRecognitionSettingsService settings) : ISmartBpCropChangeDetector
{
    private readonly object _gate = new();
    private readonly Dictionary<SmartBpRecognitionRegion, byte[]> _previous = [];
    private readonly Dictionary<SmartBpRecognitionRegion, int> _stableCounts = [];

    public SmartBpCropChangeResult Analyze(SmartBpRecognitionRegion region, BitmapSource crop, long sequence)
    {
        var sample = Sample(crop);
        lock (_gate)
        {
            var difference = _previous.TryGetValue(region, out var previous) ? Difference(previous, sample) : 1;
            var changed = difference >= settings.Settings.RecognitionCropChangeThreshold;
            _stableCounts[region] = changed ? 0 : _stableCounts.GetValueOrDefault(region) + 1;
            _previous[region] = sample;
            return new(region, sequence, difference, changed, _stableCounts[region] >= settings.Settings.RecognitionCropStableFrames);
        }
    }

    private static byte[] Sample(BitmapSource source)
    {
        var width = Math.Max(1, Math.Min(32, source.PixelWidth));
        var height = Math.Max(1, Math.Min(18, source.PixelHeight));
        var scaled = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform((double)width / source.PixelWidth, (double)height / source.PixelHeight));
        var converted = new FormatConvertedBitmap(scaled, System.Windows.Media.PixelFormats.Gray8, null, 0);
        var pixels = new byte[width * height];
        converted.CopyPixels(pixels, width, 0);
        return pixels;
    }

    private static double Difference(byte[] left, byte[] right)
    {
        var count = Math.Min(left.Length, right.Length);
        if (count == 0) return 1;
        long sum = 0;
        for (var i = 0; i < count; i++) sum += Math.Abs(left[i] - right[i]);
        return sum / (count * 255.0);
    }
}

internal sealed class SmartBpAiSnapshotDeltaRecognitionService(
    ISmartBpImageEncoder encoder,
    ISmartBpRecognitionFrameCropper cropper,
    ILlamaCppOpenAiClient client,
    ISmartBpRecognitionSettingsService settings,
    ISharedDataService shared,
    ISmartBpCropChangeDetector cropChangeDetector) : ISmartBpSnapshotDeltaRecognitionService
{
    public async Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string> { $"Frame sequence {frameSequence}: requested fields [{string.Join(", ", request.RequestedFields)}]." };
        var crops = new List<SmartBpCroppedFrame>();
        var inputs = new List<SmartBpMultimodalRegionInput>();
        var effectiveRegions = new List<(SmartBpRecognitionRegion Region, string TargetField)>();
        var phaseCrop = await CropAsync(frame, SmartBpRecognitionRegion.PhaseTop, cancellationToken);
        var phaseChange = cropChangeDetector.Analyze(SmartBpRecognitionRegion.PhaseTop, phaseCrop.Image, frameSequence);
        diagnostics.Add($"Crop change {phaseChange.Region}: diff={phaseChange.Difference:0.0000}; changed={phaseChange.IsChanged}; stable={phaseChange.IsStable}.");
        inputs.Add(new("phase_top", SmartBpRecognitionRegion.PhaseTop, "phase", await EncodeAsync(phaseCrop.Image, settings.Settings.PhaseCropMaxImageWidth, cancellationToken)));
        foreach (var (region, targetField) in request.RequestedRegions)
        {
            var crop = await CropAsync(frame, region, cancellationToken);
            crops.Add(crop);
            var change = cropChangeDetector.Analyze(region, crop.Image, frameSequence);
            diagnostics.Add($"Crop change {change.Region}: diff={change.Difference:0.0000}; changed={change.IsChanged}; stable={change.IsStable}.");
            if (!change.IsChanged && change.IsStable)
            {
                diagnostics.Add($"Skipped unchanged stable crop {region}; local state will preserve {targetField}.");
                continue;
            }
            inputs.Add(new(ToRegionId(region), region, targetField, await EncodeAsync(crop.Image, settings.Settings.ContentCropMaxImageWidth, cancellationToken)));
            effectiveRegions.Add((region, targetField));
        }

        var effectiveRequest = new SmartBpSnapshotDeltaRequest(effectiveRegions, request.Diagnostics);
        var raw = await client.RecognizeSnapshotDeltaAsync(inputs, effectiveRequest, cancellationToken);
        var parsed = SmartBpAutomaticParser.ParseSnapshotDelta(raw, effectiveRequest.RequestedFields,
            shared.SurCharaDict.Keys.ToArray(), shared.HunCharaDict.Keys.ToArray());
        diagnostics.Add($"Delta recognized phase={parsed.Phase}; updates=[{string.Join(", ", parsed.Updates.Select(x => x.Field))}].");
        return (parsed, new Dictionary<string, string> { ["snapshot_delta"] = raw }, phaseCrop, crops, diagnostics);
    }

    private async Task<SmartBpCroppedFrame> CropAsync(BitmapSource frame, SmartBpRecognitionRegion region, CancellationToken cancellationToken) =>
        await Task.Run(() => cropper.CropWithInfo(frame, region), cancellationToken);

    private async Task<string> EncodeAsync(BitmapSource image, int maxWidth, CancellationToken cancellationToken) =>
        await Task.Run(() => encoder.EncodeDataUrl(image, Math.Min(settings.Settings.MaxImageWidth, maxWidth)), cancellationToken);

    private static string ToRegionId(SmartBpRecognitionRegion region) => region switch
    {
        SmartBpRecognitionRegion.PhaseTop => "phase_top",
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => region.ToString()
    };
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

    public SmartBpRecognitionLedgerSnapshot GetSnapshot()
    {
        lock (_gate) return new(_completed.ToArray());
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
