using System.ComponentModel;
using System.Text;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 将阶段识别和各区域聚焦识别结果合并成完整 BP 业务状态。
/// </summary>
internal sealed class SmartBpBusinessStateMerger : ISmartBpBusinessStateMerger
{
    /// <inheritdoc />
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

/// <summary>
/// 持有 SmartBP 自动识别的最新业务状态，并按帧序合并字段增量。
/// </summary>
internal sealed class SmartBpRecognitionStateStore : ISmartBpRecognitionStateStore
{
    private readonly object _gate = new();
    private SmartBpRecognitionState _state = new();

    /// <inheritdoc />
    public SmartBpBusinessStateRecognitionResult Snapshot
    {
        get
        {
            lock (_gate) return ToSnapshot(_state);
        }
    }

    /// <inheritdoc />
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
                ApplyFieldUpdateLocked(update, frameSequence, diagnostics);
                _state.FieldFrameSequences[update.Field] = frameSequence;
                _state.FieldUpdatedAt[update.Field] = timestamp;
            }
        }
        return diagnostics;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ApplyFieldSnapshot(string field, SmartBpSnapshotFieldUpdate snapshot, long frameSequence, DateTimeOffset timestamp)
    {
        var diagnostics = new List<string>();
        lock (_gate)
        {
            if (_state.FieldFrameSequences.TryGetValue(field, out var existing) && frameSequence < existing)
            {
                diagnostics.Add($"Ignored stale field snapshot {field} from frame sequence {frameSequence}; latest={existing}.");
                return diagnostics;
            }
            ApplyFieldUpdateLocked(snapshot, frameSequence, diagnostics);
            _state.FieldFrameSequences[field] = frameSequence;
            _state.FieldUpdatedAt[field] = timestamp;
        }
        return diagnostics;
    }

    /// <inheritdoc />
    public void ApplyPhase(string phase, long frameSequence)
    {
        lock (_gate)
        {
            _state.Phase = phase;
            _state.LastFrameSequence = Math.Max(_state.LastFrameSequence, frameSequence);
        }
    }

    /// <summary>
    /// 分配证据在 frame sequence / freshness 跟踪中使用的独立键。
    /// 与权威 <c>picked_sur</c> 分离，避免仅更新分配证据时让权威字段看起来新鲜。
    /// </summary>
    internal const string DistributionEvidenceFieldKey = "picked_sur_distribution_evidence";

    /// <inheritdoc />
    public IReadOnlyList<string> ApplyDistributionEvidence(SmartBpSnapshotFieldUpdate update, long frameSequence, DateTimeOffset timestamp)
    {
        var diagnostics = new List<string>();
        lock (_gate)
        {
            if (_state.FieldFrameSequences.TryGetValue(DistributionEvidenceFieldKey, out var existing) && frameSequence < existing)
            {
                diagnostics.Add($"Ignored stale distribution evidence from frame sequence {frameSequence}; latest={existing}.");
                return diagnostics;
            }
            var slots = update.Slots ?? [];
            _state.DistributionEvidence = slots
                .Where(slot => slot.Index is >= 0 and < 4)
                .Select(slot => new SmartBpRecognizedPlayerCharacterSlot
                {
                    Index = slot.Index,
                    CharacterName = slot.CharacterName,
                    PlayerId = slot.PlayerId
                })
                .ToList();
            _state.DistributionEvidence.Sort((left, right) => left.Index.CompareTo(right.Index));
            _state.FieldFrameSequences[DistributionEvidenceFieldKey] = frameSequence;
            _state.FieldUpdatedAt[DistributionEvidenceFieldKey] = timestamp;
            diagnostics.Add($"Replaced distribution evidence with {_state.DistributionEvidence.Count} visual slot(s) from frame {frameSequence}; authoritative picked_sur freshness unchanged.");
        }
        return diagnostics;
    }

    private void ApplyFieldUpdateLocked(SmartBpSnapshotFieldUpdate update, long frameSequence, List<string> diagnostics)
    {
        switch (update.Field)
        {
            case "banned_sur":
                if (update.Slots != null) MergeCharacterSlots(_state.BannedSur, update.Slots, update.Field, frameSequence, diagnostics);
                break;
            case "banned_hun":
                if (update.Slots != null) MergeCharacterSlots(_state.BannedHun, update.Slots, update.Field, frameSequence, diagnostics);
                break;
            case "picked_sur":
                if (update.Slots != null) MergePlayerSlots(_state.PickedSur, update.Slots, update.Field, frameSequence, diagnostics);
                break;
            case "picked_hun":
                if (update.PickedHun != null) MergePickedHunter(_state, update.PickedHun, frameSequence, diagnostics);
                break;
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
            PickedHun = ClonePlayerSlot(state.PickedHun),
            DistributionEvidence = state.DistributionEvidence.Select(ClonePlayerSlot).ToList()
        };
        SmartBpBusinessStateParser.NormalizeAndValidate(snapshot);
        return snapshot;
    }

    private static void MergeCharacterSlots(List<SmartBpRecognizedCharacterSlot> current, IEnumerable<SmartBpSnapshotDeltaSlot> updates, string field, long frameSequence, List<string> diagnostics)
    {
        foreach (var update in updates.OrderBy(x => x.Index))
        {
            var slot = current.FirstOrDefault(x => x.Index == update.Index);
            if (slot == null)
            {
                slot = new SmartBpRecognizedCharacterSlot { Index = update.Index };
                current.Add(slot);
            }
            switch (update.SlotState)
            {
                case "selected":
                    slot.CharacterName = update.CharacterName;
                    diagnostics.Add($"Applied {field}[{update.Index}] = {update.CharacterName} from frame {frameSequence}.");
                    break;
                case "empty":
                    slot.CharacterName = "未选择";
                    diagnostics.Add($"Cleared {field}[{update.Index}] because slot_state=empty.");
                    break;
                case "unknown":
                    diagnostics.Add($"Preserved {field}[{update.Index}] because slot_state=unknown.");
                    break;
            }
        }
        current.Sort((left, right) => left.Index.CompareTo(right.Index));
    }

    private static void MergePlayerSlots(List<SmartBpRecognizedPlayerCharacterSlot> current, IEnumerable<SmartBpSnapshotDeltaSlot> updates, string field, long frameSequence, List<string> diagnostics)
    {
        foreach (var update in updates.OrderBy(x => x.Index))
        {
            var slot = current.FirstOrDefault(x => x.Index == update.Index);
            if (slot == null)
            {
                slot = new SmartBpRecognizedPlayerCharacterSlot { Index = update.Index };
                current.Add(slot);
            }
            switch (update.SlotState)
            {
                case "selected":
                    slot.CharacterName = update.CharacterName;
                    slot.PlayerId = update.PlayerId;
                    diagnostics.Add($"Applied {field}[{update.Index}] = {update.CharacterName}{FormatPlayer(update.PlayerId)} from frame {frameSequence}.");
                    break;
                case "empty":
                    slot.CharacterName = "未选择";
                    slot.PlayerId = update.PlayerId ?? slot.PlayerId;
                    diagnostics.Add($"Cleared {field}[{update.Index}] because slot_state=empty; player_id={(slot.PlayerId ?? "null")}.");
                    break;
                case "unknown":
                    diagnostics.Add($"Preserved {field}[{update.Index}] because slot_state=unknown.");
                    break;
            }
        }
        current.Sort((left, right) => left.Index.CompareTo(right.Index));
    }

    private static void MergePickedHunter(SmartBpRecognitionState state, SmartBpSnapshotDeltaSlot update, long frameSequence, List<string> diagnostics)
    {
        switch (update.SlotState)
        {
            case "selected":
                state.PickedHun = new SmartBpRecognizedPlayerCharacterSlot { Index = 0, CharacterName = update.CharacterName, PlayerId = update.PlayerId };
                diagnostics.Add($"Applied picked_hun[0] = {update.CharacterName}{FormatPlayer(update.PlayerId)} from frame {frameSequence}.");
                break;
            case "empty":
                state.PickedHun.CharacterName = "未选择";
                state.PickedHun.PlayerId = update.PlayerId ?? state.PickedHun.PlayerId;
                diagnostics.Add("Cleared picked_hun[0] because slot_state=empty.");
                break;
            case "unknown":
                diagnostics.Add("Preserved picked_hun[0] because slot_state=unknown.");
                break;
        }
    }

    private static string FormatPlayer(string? playerId) => string.IsNullOrWhiteSpace(playerId) ? "" : $" / player_id={playerId}";

    private static SmartBpRecognizedCharacterSlot CloneCharacterSlot(SmartBpRecognizedCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason };

    private static SmartBpRecognizedPlayerCharacterSlot ClonePlayerSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, PlayerId = slot.PlayerId, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason };
}

/// <summary>
/// 根据当前引导步骤、本地状态和已完成台账规划本轮需要识别的区域字段。
/// </summary>
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
        {
            var fullRequest = new SmartBpSnapshotDeltaRequest(
            [
                (SmartBpRecognitionRegion.RightTop, "banned_sur"),
                (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
                (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
                (SmartBpRecognitionRegion.RightBottom, "picked_hun")
            ], ["Free full sync initially considers every character region."], currentLocalSnapshot);
            var freeSyncDiagnostics = fullRequest.Diagnostics.ToList();
            return SmartBpAutoRecognitionCoordinator.FilterAutomaticRequestByPhase(
                fullRequest, currentLocalSnapshot.Phase, freeSyncDiagnostics) with { Diagnostics = freeSyncDiagnostics };
        }
        var requested = new Dictionary<string, SmartBpRecognitionRegion>(StringComparer.Ordinal);
        var diagnostics = new List<string>();
        void Add(string field, SmartBpRecognitionRegion region, string reason)
        {
            if (requested.TryAdd(field, region)) diagnostics.Add($"Request {field} ({region}): {reason}");
        }

        if (guidanceSnapshot.IsStarted && guidanceSnapshot.Workflow.Count > 0)
        {
            var lookBehind = Math.Max(0, settings.Settings.OcrBackfillLookBehindSteps);
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

        var staleMilliseconds = settings.Settings.OcrFieldStaleMilliseconds;
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
        var planned = new SmartBpSnapshotDeltaRequest(
            requested.Select(item => (item.Value, item.Key)).ToArray(), diagnostics, currentLocalSnapshot);
        return SmartBpAutoRecognitionCoordinator.FilterAutomaticRequestByPhase(
            planned, currentLocalSnapshot.Phase, diagnostics) with { Diagnostics = diagnostics };

        void AddForAction(GameAction action, string reason)
        {
            if (!SmartBpAutomaticMapping.IsCharacterOperationAction(action)) return;
            var (region, field) = SmartBpAutomaticMapping.GetFocusedTarget(action);
            Add(field, region, reason);
        }
    }
}

/// <summary>
/// 保留最近若干帧，供阶段转场和回填逻辑选择更合适的截图。
/// </summary>
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

/// <summary>
/// 对裁剪图进行低分辨率采样，判断区域画面是否发生足够变化。
/// </summary>
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

/// <summary>
/// 记录已应用或跳过的工作流操作，避免自动识别重复写入同一步骤。
/// </summary>
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

/// <summary>
/// 根据当前快照和工作流步骤生成可回填的候选操作计划。
/// </summary>
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
