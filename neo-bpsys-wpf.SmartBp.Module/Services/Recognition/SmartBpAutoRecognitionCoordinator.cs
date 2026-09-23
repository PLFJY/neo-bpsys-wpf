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

internal sealed class SmartBpAutoRecognitionCoordinator(
    ISmartBpSnapshotRecognitionPlanner planner,
    ISmartBpFrameRingBuffer frameRingBuffer,
    ISmartBpRecognitionSettingsService settings,
    IGameGuidanceService guidance,
    ICharacterSelectionService selection,
    SmartBpCandidateOperationBuilder candidateBuilder,
    ISmartBpSceneGateService sceneGate,
    ISmartBpOcrBpRecognitionService ocrRecognition,
    SmartBpHistoricalFrameReviewService historicalReview,
    ISmartBpReconciliationService reconciliation) : ISmartBpAutoRecognitionCoordinator
{
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private readonly object _cancellationLock = new();
    private CancellationTokenSource? _runCancellation;
    private CancellationTokenSource? _currentTickCancellation;
    private string? _lastSnapshotFingerprint;
    private int _stableSnapshotCount;
    private long _frameSequence;
    private bool _hasDetectedPostBp;
    private string _postBpPhase = "未知";
    private long _postBpDetectedFrameSequence;
    private int _transitionToAreaSelectionConsecutiveCount;
    private SmartBpLifecycleCategory _lastStableLifecycleCategory = SmartBpLifecycleCategory.Unknown;
    public bool IsRunning => _runCancellation is { IsCancellationRequested: false };

    /// <inheritdoc />
    public void SampleFrame(BitmapSource frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var sequence = Interlocked.Increment(ref _frameSequence);
        frameRingBuffer.AddFrame(sequence, frame, DateTimeOffset.Now);
    }

    /// <inheritdoc />
    public void ResetCaptureContext()
    {
        CancelCurrentAutomaticTick();
        frameRingBuffer.Reset();
        _lastSnapshotFingerprint = null;
        _stableSnapshotCount = 0;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_cancellationLock)
        {
            _runCancellation?.Cancel();
            _runCancellation?.Dispose();
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }
        _lastSnapshotFingerprint = null;
        _stableSnapshotCount = 0;
        ClearPostBpLatch();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_cancellationLock)
        {
            _runCancellation?.Cancel();
            _currentTickCancellation?.Cancel();
        }
        ClearPostBpLatch();
        return Task.CompletedTask;
    }

    public Task CompleteAsync()
    {
        lock (_cancellationLock)
        {
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
        ClearPostBpLatch();
        return Task.CompletedTask;
    }

    public async Task<SmartBpAutoRecognitionTickResult> RunOneTickAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: false, cancellationToken).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RunOneTickDryRunAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: true, cancellationToken).ConfigureAwait(false);

    // OCR-only 执行矩阵：完整调试 OCR 所有 BP 字段；仅阶段识别只 OCR 状态/阶段；自动模式使用规划器请求的字段。
    public async Task<SmartBpAutoRecognitionTickResult> RunFullRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RecognizeFullBpSnapshotAsync(frame, isDryRun: false, cancellationToken).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RecognizeFullBpSnapshotAsync(
        BitmapSource frame,
        bool isDryRun,
        CancellationToken cancellationToken = default)
    {
        CancelCurrentAutomaticTick();
        return await RunOneTickCoreAsync(
            frame,
            isDryRun,
            cancellationToken,
            SmartBpRecognitionDebugMode.FullStrategy,
            linkToAutomaticRunCancellation: false,
            waitForRunningTick: true).ConfigureAwait(false);
    }

    public async Task<SmartBpAutoRecognitionTickResult> RunIncrementalRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunOneTickCoreAsync(frame, isDryRun: false, cancellationToken, SmartBpRecognitionDebugMode.CurrentStageIncremental).ConfigureAwait(false);

    public async Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default)
        => await RunPhaseOnlyDebugCoreAsync(frame, cancellationToken).ConfigureAwait(false);

    private async Task<SmartBpAutoRecognitionTickResult> RunOneTickCoreAsync(
        BitmapSource frame,
        bool isDryRun,
        CancellationToken cancellationToken = default,
        SmartBpRecognitionDebugMode debugMode = SmartBpRecognitionDebugMode.Automatic,
        bool linkToAutomaticRunCancellation = true,
        bool waitForRunningTick = false)
    {
        var gateAcquired = waitForRunningTick
            ? await _tickGate.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false)
            : await _tickGate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
        if (!gateAcquired)
            return Failure("An automatic recognition tick is already running.");
        var tickStartedAt = Stopwatch.GetTimestamp();
        CancellationTokenSource linked;
        lock (_cancellationLock)
        {
            linked = linkToAutomaticRunCancellation
                ? CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _runCancellation?.Token ?? CancellationToken.None)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (linkToAutomaticRunCancellation)
                _currentTickCancellation = linked;
        }
        var tickToken = linked.Token;
        var isDebugPreview = debugMode != SmartBpRecognitionDebugMode.Automatic;
        string raw = "";
        try
        {
            var sequence = Interlocked.Increment(ref _frameSequence);
            var captureTimestamp = DateTimeOffset.Now;
            if (!isDebugPreview && !isDryRun && _hasDetectedPostBp)
                return CreateLatchedPostBpResult(sequence);
            frameRingBuffer.AddFrame(sequence, frame, captureTimestamp);
            var guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var request = debugMode == SmartBpRecognitionDebugMode.FullStrategy
                ? BuildFullStrategyDebugRequest(CreateCurrentFrameState("未知"))
                : planner.BuildRequest(guidanceSnapshot);
            SmartBpRegionSnapshot? regionSnapshot = null;
            SmartBpPhaseRecognitionResult phaseResult;
            SmartBpCroppedFrame? phaseCrop;
            IReadOnlyList<SmartBpCroppedFrame> contentCrops;
            SmartBpBusinessStateRecognitionResult state = CreateCurrentFrameState("未知");
            var messages = new List<string>(request.Diagnostics);
            if (isDryRun)
                messages.Add("Speed-test dry run: recognition request shape matches automatic tick, but local merge, auto apply, and GameGuidance sync are disabled.");
            if (debugMode == SmartBpRecognitionDebugMode.FullStrategy)
                messages.Add("Full strategy debug: phase_top plus all four BP business fields are requested.");
            else if (debugMode == SmartBpRecognitionDebugMode.CurrentStageIncremental)
                messages.Add("Current-stage incremental debug: automatic planner requested only relevant/stale fields; operation apply and guidance sync are disabled.");
            IReadOnlyDictionary<string, string> rawResponses;
            var recognitionPath = ResolveRecognitionPath(request);
            messages.Add($"Recognition path: {recognitionPath}; requested_fields=[{string.Join(", ", request.RequestedFields)}].");
            if (!isDebugPreview && !isDryRun)
            {
                var localStatus = await ocrRecognition.RecognizeAsync(
                    frame,
                    new SmartBpOcrRecognitionRequest(
                        [SmartBpRecognitionRegion.TopCenterStatus, SmartBpRecognitionRegion.TopLeftStatus],
                        IncludePhase: false),
                    tickToken).ConfigureAwait(false);
                messages.AddRange(localStatus.Diagnostics);
                var statusRaw = string.Join(Environment.NewLine, localStatus.Regions
                    .Where(region => region.Region is SmartBpRecognitionRegion.TopCenterStatus or SmartBpRecognitionRegion.TopLeftStatus)
                    .SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
                var lifecycle = localStatus.LifecycleStatus;
                if (localStatus.PostBpStatus?.IsPostBp == true || IsPrimaryPostBpPhase(localStatus.Phase.Phase))
                {
                    phaseResult = new SmartBpPhaseRecognitionResult { Phase = localStatus.Phase.Phase };
                    state = CreateCurrentFrameState(phaseResult.Phase);
                    var statusGate = sceneGate.Classify(phaseResult, state,
                        new Dictionary<string, string> { ["top_left_status"] = statusRaw }, guidanceSnapshot);
                    messages.Add($"TopLeftStatus hard confirmation: {phaseResult.Phase}; final_phase={phaseResult.Phase}.");
                    return LatchAndCreatePostBpPausedResult(
                        sequence, state, phaseResult, null, guidanceSnapshot, messages, statusRaw, statusGate);
                }

                if (lifecycle != null)
                {
                    messages.Add($"TopCenterStatus lifecycle gate: status={lifecycle.Status}; category={lifecycle.Category}; score={lifecycle.Score:0.00}.");
                    if (lifecycle.Category == SmartBpLifecycleCategory.TransitionToAreaSelection)
                    {
                        _transitionToAreaSelectionConsecutiveCount++;
                        var confirmed = lifecycle.Score >= .80 || _transitionToAreaSelectionConsecutiveCount >= 2;
                        if (confirmed)
                        {
                            phaseResult = new SmartBpPhaseRecognitionResult { Phase = "即将进入区域选择" };
                            state = CreateCurrentFrameState(phaseResult.Phase);
                            var transitionGate = new SmartBpSceneGateResult(
                                SmartBpRecognitionScene.OutOfBp, false, false, true,
                                "top-center lifecycle reached transition to area selection");
                            messages.Add("content_recognition_allowed=False; post_bp_stop_pending=True; no new BP field recognition will run.");
                            messages.Add($"Post-BP latch set by TopCenterStatus: 即将进入区域选择. confirmation={(lifecycle.Score >= .80 ? "strong score" : "two consecutive weak matches")}.");
                            return LatchAndCreatePostBpPausedResult(sequence, state, phaseResult, null,
                                guidanceSnapshot, messages, statusRaw, transitionGate);
                        }

                        messages.Add("Weak TransitionToAreaSelection match blocked content recognition for this tick; waiting for confirmation; no stop requested.");
                        return CreateLifecycleBlockedResult(sequence, messages, statusRaw,
                            "weak transition match is awaiting a second tick or TopLeftStatus confirmation");
                    }

                    _transitionToAreaSelectionConsecutiveCount = 0;
                    if (lifecycle.IsRecognized)
                    {
                        _lastStableLifecycleCategory = lifecycle.Category;
                        request = FilterAutomaticRequestByLifecycle(request, lifecycle.Category, messages);
                        if ((lifecycle.Category is SmartBpLifecycleCategory.SurvivorTalentAdjust or SmartBpLifecycleCategory.HunterTalentAdjust) &&
                            request.RequestedRegions.Count == 0)
                            return CreateLifecycleBlockedResult(sequence, messages, statusRaw,
                                $"{lifecycle.Category} has no safe configured final-backfill field");
                        messages.Add($"content_recognition_allowed=True; lifecycle_safe_fields=[{string.Join(", ", request.RequestedFields)}].");
                    }
                    else if (_lastStableLifecycleCategory != SmartBpLifecycleCategory.CharacterBpActive)
                    {
                        messages.Add($"TopCenterStatus lifecycle uncertain: best_match={lifecycle.Status} score={lifecycle.Score:0.00}; skipped content recognition for this tick; no stop requested.");
                        return CreateLifecycleBlockedResult(sequence, messages, statusRaw,
                            "top-center lifecycle status is uncertain");
                    }
                    else
                    {
                        messages.Add("TopCenterStatus lifecycle uncertain; safe previous stable CharacterBpActive state allows this tick to continue.");
                    }
                }
            }
            if (!isDebugPreview)
            {
                var phaseOnlyOcr = await ocrRecognition.RecognizeAsync(
                    frame,
                    new SmartBpOcrRecognitionRequest([], IncludePhase: true),
                    tickToken).ConfigureAwait(false);
                var phaseRaw = string.Join(Environment.NewLine, phaseOnlyOcr.Regions
                    .Where(region => region.Region == SmartBpRecognitionRegion.PhaseTop)
                    .SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
                phaseResult = phaseOnlyOcr.Phase;
                phaseCrop = null;
                state = CreateCurrentFrameState(phaseResult.Phase);
                rawResponses = new Dictionary<string, string> { ["ocr_phase"] = phaseRaw };
                messages.AddRange(phaseOnlyOcr.Diagnostics);
                var preContentGate = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
                messages.Add(preContentGate.ShouldPauseAutomaticRecognition
                    ? $"Post-BP phase detected: phase={phaseResult.Phase}; scene={preContentGate.Scene}."
                    : $"OCR phase gate: phase={phaseResult.Phase}; scene={preContentGate.Scene}.");
                if (preContentGate.ShouldPauseAutomaticRecognition)
                    return LatchAndCreatePostBpPausedResult(sequence,
                        state, phaseResult, phaseCrop, guidanceSnapshot, messages, phaseRaw, preContentGate);
                request = FilterAutomaticRequestByPhase(request, phaseResult.Phase, messages);
                messages.Add("OCR phase gate allowed BP content recognition.");
            }
            else
            {
                var phaseOnlyOcr = await ocrRecognition.RecognizeAsync(
                    frame,
                    new SmartBpOcrRecognitionRequest([], IncludePhase: true),
                    tickToken).ConfigureAwait(false);
                phaseResult = phaseOnlyOcr.Phase;
                phaseCrop = null;
                raw = string.Join(Environment.NewLine, phaseOnlyOcr.Regions
                    .SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
                rawResponses = new Dictionary<string, string> { ["ocr_phase"] = raw };
                messages.AddRange(phaseOnlyOcr.Diagnostics);
            }

            var gateBeforeContent = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
            if ((!gateBeforeContent.IsBpRecognitionAllowed && !isDebugPreview) || request.RequestedRegions.Count == 0)
            {
                contentCrops = [];
                messages.Add(request.RequestedRegions.Count == 0
                    ? "OCR skipped content recognition because no fields were requested."
                    : $"OCR skipped content recognition because BP recognition is blocked by the scene decision: {gateBeforeContent.Reason}.");
            }
            else
            {
                var tickMode = DescribeRecognitionTickMode(debugMode);
                messages.Add($"OCR role-distribution diagnostics: saved_frame_id={sequence}; tick_mode={tickMode}; planner_requested_fields=[{string.Join(", ", request.RequestedFields)}]; ocr_requested_regions=[{string.Join(", ", request.RequestedRegions.Select(item => $"{item.Region}->{item.TargetField}"))}].");
                var ocrParseContext = new SmartBpOcrFieldParseContext
                {
                    AuthoritativePhase = phaseResult.Phase,
                    CurrentGuidanceAction = debugMode == SmartBpRecognitionDebugMode.FullStrategy
                        ? null
                        : guidanceSnapshot.CurrentAction,
                    SurvivorPickLocked = debugMode != SmartBpRecognitionDebugMode.FullStrategy &&
                                         SmartBpAutomaticMapping.IsSurvivorPickLocked(guidanceSnapshot, phaseResult.Phase),
                    IsAutomaticMode = !isDebugPreview,
                    IsGlobalSnapshot = debugMode == SmartBpRecognitionDebugMode.FullStrategy
                };
                var ocr = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(
                    request.RequestedRegions.Select(item => item.Region).Distinct().ToArray(),
                    IncludePhase: false,
                    ParseContext: ocrParseContext), tickToken);
                var ocrRaw = string.Join(Environment.NewLine, ocr.Regions.SelectMany(region =>
                    region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text} conf={line.Confidence:0.00}")));
                raw = string.IsNullOrWhiteSpace(raw) ? ocrRaw : raw + "\n\nocr raw:\n" + ocrRaw;
                rawResponses = new Dictionary<string, string>(rawResponses) { ["ocr"] = ocrRaw };
                messages.Add($"OCR role-distribution diagnostics: phase_result={phaseResult.Phase}; ocr_raw_lines=[{string.Join(" | ", ocr.Regions.SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")))}].");
                messages.Add($"OCR current-frame state={FormatBusinessStateForDiagnostics(ocr.BusinessState)}.");
                messages.AddRange(ocr.Diagnostics);
                if (!string.Equals(ocr.BusinessState.Phase, phaseResult.Phase, StringComparison.Ordinal))
                {
                    messages.Add($"OCR content phase={ocr.BusinessState.Phase} ignored; authoritative phase gate remains {phaseResult.Phase}.");
                    ocr.BusinessState.Phase = phaseResult.Phase;
                }
                state = ocr.BusinessState;
                messages.AddRange(ApplyCurrentFrameGuards(state, phaseResult.Phase, guidanceSnapshot));
                messages.Add($"OCR guarded current-frame state={FormatBusinessStateForDiagnostics(state)}.");
                contentCrops = [];
            }

            guidanceSnapshot = guidance.GetRuntimeSnapshot();
            var gate = sceneGate.Classify(phaseResult, state, rawResponses, guidanceSnapshot);
            messages.Add($"Scene: {gate.Scene}; BP recognition allowed: {gate.IsBpRecognitionAllowed}; Character operations allowed: {gate.IsCharacterOperationAllowed}; Action: {(gate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}; Reason: {gate.Reason}.");
            if (!isDebugPreview && !isDryRun && gate.ShouldPauseAutomaticRecognition)
                return LatchAndCreatePostBpPausedResult(sequence, state, phaseResult, phaseCrop,
                    guidanceSnapshot, messages, raw, gate);
            if (isDryRun)
            {
                var dryRunSync = new SmartBpGuidanceSyncResult(false, false, "Speed-test dry run: GameGuidance synchronization is disabled.", null, [], null);
                var dryRunApply = new SmartBpOperationApplyResult(0, 0, ["Speed-test dry run: character operation application is disabled."]);
                var dryRunSnapshot = regionSnapshot ?? new SmartBpRegionSnapshot
                {
                    Phase = phaseResult,
                    BusinessState = state,
                    Diagnostics = messages,
                    PhaseCrop = phaseCrop,
                    ContentCrops = contentCrops,
                    RawResponses = new Dictionary<string, string> { ["snapshot_delta"] = raw }
                };
                return new(state, phaseResult, null, phaseCrop, null, dryRunSync, guidanceSnapshot,
                    [], messages, dryRunApply, raw, null, dryRunSnapshot, contentCrops, gate);
            }

            SmartBpCatchUpTriggerDecision? catchUpTrigger = null;
            var requiresGuidanceStart = !guidanceSnapshot.IsStarted &&
                                        settings.Settings.EnableAutoGuidanceSync &&
                                        SmartBpAutomaticMapping.TryMapPhase(state.Phase, out _);
            if (!isDebugPreview && gate.IsCharacterOperationAllowed &&
                (settings.Settings.EnableAutoApplyRecognition || settings.Settings.EnableAutoGuidanceSync))
            {
                catchUpTrigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                    guidanceSnapshot,
                    state,
                    selection.GetCurrentBpSlotCommitState());
                messages.Add($"Automatic catch-up precheck: {catchUpTrigger.Reason}.");
                if (catchUpTrigger.ShouldReviewHistory)
                {
                    var review = await historicalReview.SupplementAsync(
                        state,
                        sequence,
                        guidanceSnapshot,
                        tickToken).ConfigureAwait(false);
                    state = review.State;
                    messages.AddRange(review.Diagnostics);
                    messages.Add($"Historical review summary: reviewed_frames={review.ReviewedFrameCount}; supplemented_slots={review.SupplementedSlotCount}; merge_mode=supplement-only.");
                    catchUpTrigger = SmartBpCatchUpTriggerEvaluator.Evaluate(
                        guidanceSnapshot,
                        state,
                        selection.GetCurrentBpSlotCommitState());
                }
                else
                {
                    messages.Add("Historical review not triggered; no historical OCR was scheduled.");
                }
            }

            var operations = gate.IsCharacterOperationAllowed
                ? BuildCurrentFrameOperations(state, candidateBuilder)
                : [];
            var distributionOperations = gate.IsCharacterOperationAllowed &&
                                         catchUpTrigger?.TargetStep?.Action == GameAction.DistributeChara
                ? candidateBuilder.BuildWithDiagnostics(
                    state,
                    GameAction.DistributeChara,
                    catchUpTrigger.TargetStep.Indexes).Operations.ToArray()
                : [];
            if (distributionOperations.Length > 0)
                operations = operations.Concat(distributionOperations).ToArray();
            var fingerprint = JsonSerializer.Serialize(state);
            _stableSnapshotCount = string.Equals(_lastSnapshotFingerprint, fingerprint, StringComparison.Ordinal)
                ? _stableSnapshotCount + 1
                : 1;
            _lastSnapshotFingerprint = fingerprint;
            var requiredStable = Math.Max(1, settings.Settings.RequiredStableSnapshots);
            var shouldRunReconciliation = requiresGuidanceStart ||
                                          catchUpTrigger?.ShouldReconcile == true ||
                                          distributionOperations.Length > 0;
            SmartBpReconciliationResult? reconciliationResult = isDebugPreview
                ? null
                : gate.IsCharacterOperationAllowed &&
                  (settings.Settings.EnableAutoApplyRecognition || settings.Settings.EnableAutoGuidanceSync) &&
                  shouldRunReconciliation &&
                  _stableSnapshotCount >= requiredStable
                    ? await reconciliation.ReconcileAsync(state, SmartBpReconciliationMode.Automatic, tickToken)
                    : null;
            if (reconciliationResult is not null)
                messages.AddRange(reconciliationResult.Diagnostics);
            SmartBpOperationApplyResult applyResult = isDebugPreview
                ? new(0, operations.Length, ["Recognition debug preview: operation application is disabled."])
                : reconciliationResult is not null
                    ? new(
                        reconciliationResult.CharacterApplyResult.AppliedCount + reconciliationResult.EmptyApplyResult.AppliedCount,
                        reconciliationResult.CharacterApplyResult.SkippedCount + reconciliationResult.EmptyApplyResult.SkippedCount,
                        reconciliationResult.CharacterApplyResult.Messages.Concat(reconciliationResult.EmptyApplyResult.Messages).ToArray())
                : !shouldRunReconciliation
                    ? new(0, operations.Length, ["Skipped: automatic catch-up trigger was not met; Action/Indexes are aligned and no Pending/CommittedEmpty slot has new role evidence."])
                : settings.Settings.EnableAutoApplyRecognition
                    ? new(0, operations.Length, [$"Skipped: waiting for stable BP observations ({_stableSnapshotCount}/{requiredStable})."])
                    : new(0, operations.Length, operations.Length == 0
                    ? ["Skipped: auto apply disabled; no candidate operations were generated."]
                    : operations.Select(x => $"Skipped: auto apply disabled for step {x.SourceWorkflowStepIndex} {x.Kind} {x.Camp}[{x.SlotIndex}] {x.RawCharacterName ?? "null"}.").ToArray());
            SmartBpGuidanceSyncResult? sync = reconciliationResult is not null && settings.Settings.EnableAutoGuidanceSync
                ? new(
                    reconciliationResult.GuidanceResult.Moved,
                    reconciliationResult.GuidanceResult.Succeeded,
                    reconciliationResult.GuidanceResult.Message,
                    reconciliationResult.GuidanceResult.TargetAction,
                    reconciliationResult.GuidanceResult.TargetIndexes,
                    reconciliationResult.GuidanceResult.TargetStepIndex)
                : new(false, !isDebugPreview && settings.Settings.EnableAutoGuidanceSync && !shouldRunReconciliation,
                    isDebugPreview
                        ? "Recognition debug preview: GameGuidance synchronization is disabled."
                        : settings.Settings.EnableAutoGuidanceSync && !shouldRunReconciliation
                            ? "Automatic catch-up was not triggered because Action/Indexes are aligned and no Pending/CommittedEmpty slot has new role evidence."
                            : "Automatic GameGuidance synchronization is disabled.", null, [], null);
            var finalGuidanceSnapshot = guidance.GetRuntimeSnapshot();
            var progressSync = reconciliationResult?.GuidanceResult;
            if (progressSync?.Moved == true)
                finalGuidanceSnapshot = guidance.GetRuntimeSnapshot();
            var snapshotForUi = regionSnapshot ?? new SmartBpRegionSnapshot
            {
                Phase = phaseResult,
                BusinessState = state,
                Diagnostics = messages,
                PhaseCrop = phaseCrop,
                ContentCrops = contentCrops,
                RawResponses = new Dictionary<string, string> { ["snapshot_delta"] = raw }
            };
            return new(state, phaseResult, null, phaseCrop, null, sync, finalGuidanceSnapshot,
                operations, messages, applyResult, raw, null, snapshotForUi, contentCrops, gate, progressSync);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, raw, ex.Message);
        }
        finally
        {
            frameRingBuffer.ReportOcrProcessingDuration(Stopwatch.GetElapsedTime(tickStartedAt));
            lock (_cancellationLock)
            {
                if (linkToAutomaticRunCancellation && ReferenceEquals(_currentTickCancellation, linked))
                    _currentTickCancellation = null;
            }
            linked.Dispose();
            _tickGate.Release();
        }
    }

    private void CancelCurrentAutomaticTick()
    {
        lock (_cancellationLock)
        {
            _currentTickCancellation?.Cancel();
        }
    }

    private SmartBpAutoRecognitionTickResult Failure(string error) =>
        new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, "", error);

    private SmartBpAutoRecognitionTickResult LatchAndCreatePostBpPausedResult(
        long frameSequence,
        SmartBpBusinessStateRecognitionResult state,
        SmartBpPhaseRecognitionResult phaseResult,
        SmartBpCroppedFrame? phaseCrop,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        ICollection<string> messages,
        string raw,
        SmartBpSceneGateResult gate)
    {
        if (!_hasDetectedPostBp)
        {
            _hasDetectedPostBp = true;
            _postBpPhase = phaseResult.Phase;
            _postBpDetectedFrameSequence = frameSequence;
            messages.Add($"Post-BP latch set: phase={_postBpPhase}; frame_sequence={_postBpDetectedFrameSequence}.");
        }
        return CreatePostBpPausedResult(state, phaseResult, phaseCrop, guidanceSnapshot, messages, raw, gate);
    }

    private SmartBpAutoRecognitionTickResult CreateLatchedPostBpResult(long frameSequence)
    {
        var phase = new SmartBpPhaseRecognitionResult { Phase = _postBpPhase };
        var state = CreateCurrentFrameState(_postBpPhase);
        var messages = new List<string>
        {
            $"Post-BP latch already set at frame_sequence={_postBpDetectedFrameSequence}; ignored recognition tick frame_sequence={frameSequence}.",
            "No BP content recognition or field merge was run."
        };
        var scene = _postBpPhase switch
        {
            "求生者选择区域中" => SmartBpRecognitionScene.AreaSelectionSurvivor,
            "监管者选择区域中" => SmartBpRecognitionScene.AreaSelectionHunter,
            "等待游戏开始" => SmartBpRecognitionScene.WaitingGameStart,
            _ => SmartBpRecognitionScene.OutOfBp
        };
        var gate = new SmartBpSceneGateResult(scene, false, false, true,
            "post-BP latch prevents recognition from resuming before automatic stop completes");
        return CreatePostBpPausedResult(state, phase, null, guidance.GetRuntimeSnapshot(), messages, "", gate);
    }

    private void ClearPostBpLatch()
    {
        _hasDetectedPostBp = false;
        _postBpPhase = "未知";
        _postBpDetectedFrameSequence = 0;
        _transitionToAreaSelectionConsecutiveCount = 0;
        _lastStableLifecycleCategory = SmartBpLifecycleCategory.Unknown;
    }

    private SmartBpAutoRecognitionTickResult CreateLifecycleBlockedResult(
        long frameSequence,
        ICollection<string> messages,
        string raw,
        string reason)
    {
        var state = CreateCurrentFrameState("未知");
        var phase = new SmartBpPhaseRecognitionResult { Phase = state.Phase };
        var gate = new SmartBpSceneGateResult(SmartBpRecognitionScene.Unknown, false, false, false, reason);
        messages.Add("content_recognition_allowed=False; no BP OCR, Business OCR fusion, field merge, or candidate operation generation was run.");
        return new(state, phase, null, null, null, null, guidance.GetRuntimeSnapshot(), [], messages.ToArray(),
            new SmartBpOperationApplyResult(0, 0, []), raw, null, null, [], gate);
    }

    private static bool IsPrimaryPostBpPhase(string phase) =>
        phase is "求生者选择区域中" or "监管者选择区域中" or "等待游戏开始";

    internal static SmartBpSnapshotDeltaRequest FilterAutomaticRequestByPhase(
        SmartBpSnapshotDeltaRequest request,
        string authoritativePhase,
        ICollection<string>? diagnostics = null)
    {
        var allowedFields = authoritativePhase switch
        {
            "屏蔽求生者" or "屏蔽监管者" or "选择求生者" or "求生者选择角色中" or
                "求生者选择天赋中" or "选择监管者" or "监管者选择天赋中" =>
                new HashSet<string>(["banned_sur", "banned_hun", "picked_sur", "picked_hun"], StringComparer.Ordinal),
            _ => new HashSet<string>(StringComparer.Ordinal)
        };
        var filtered = request.RequestedRegions
            .Where(item => allowedFields.Contains(item.TargetField))
            .ToArray();
        var removed = request.RequestedFields.Where(field => !allowedFields.Contains(field)).ToArray();
        if (removed.Length > 0)
            diagnostics?.Add($"Phase-aware field filter removed [{string.Join(", ", removed)}] because authoritative phase={authoritativePhase}.");
        if (filtered.Length == 0)
            diagnostics?.Add($"Phase-aware field filter requested no content fields for authoritative phase={authoritativePhase}.");
        return new SmartBpSnapshotDeltaRequest(filtered, request.Diagnostics, request.CurrentKnownState);
    }

    internal static SmartBpSnapshotDeltaRequest FilterAutomaticRequestByLifecycle(
        SmartBpSnapshotDeltaRequest request,
        SmartBpLifecycleCategory category,
        ICollection<string>? diagnostics = null)
    {
        HashSet<string>? allowedFields = category switch
        {
            SmartBpLifecycleCategory.CharacterBpActive => null,
            SmartBpLifecycleCategory.SurvivorTalentAdjust or SmartBpLifecycleCategory.HunterTalentAdjust =>
                new(["banned_sur", "banned_hun", "picked_sur", "picked_hun"], StringComparer.Ordinal),
            _ => new(StringComparer.Ordinal)
        };
        if (allowedFields == null) return request;
        var filtered = request.RequestedRegions.Where(item => allowedFields.Contains(item.TargetField)).ToArray();
        var removed = request.RequestedFields.Where(field => !allowedFields.Contains(field)).ToArray();
        if (removed.Length > 0)
            diagnostics?.Add($"Lifecycle-aware field filter removed [{string.Join(", ", removed)}] because category={category}.");
        return new SmartBpSnapshotDeltaRequest(filtered, request.Diagnostics, request.CurrentKnownState);
    }

    private static IReadOnlyList<string> ApplyCurrentFrameGuards(
        SmartBpBusinessStateRecognitionResult state,
        string authoritativePhase,
        GameGuidanceRuntimeSnapshot guidanceSnapshot)
    {
        state.Phase = authoritativePhase;
        return
        [
            $"Current-frame slot evidence retained without SmartBP state merge; guidanceStep={guidanceSnapshot.CurrentStepIndex}; action={guidanceSnapshot.CurrentAction}."
        ];
    }

    private static SmartBpAutoRecognitionTickResult CreatePostBpPausedResult(
        SmartBpBusinessStateRecognitionResult state,
        SmartBpPhaseRecognitionResult phaseResult,
        SmartBpCroppedFrame? phaseCrop,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        ICollection<string> messages,
        string raw,
        SmartBpSceneGateResult gate)
    {
        messages.Add($"Post-BP phase detected: phase={phaseResult.Phase}; scene={gate.Scene}.");
        messages.Add("Character BP has ended; no new recognition ticks will be scheduled.");
        messages.Add("Automatic recognition stop is queued after pending operations drain.");
        messages.Add("Skipped content field recognition because BP ended.");
        return new(state, phaseResult, null, phaseCrop, null, null, guidanceSnapshot,
            [], messages.ToArray(), new(0, 0, ["Skipped: BP ended before content recognition; no character operations were generated."]),
            raw, null, null, [], gate);
    }

    private async Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugCoreAsync(
        BitmapSource frame,
        CancellationToken cancellationToken = default)
    {
        if (!await _tickGate.WaitAsync(0, cancellationToken))
            return Failure("An automatic recognition tick is already running.");
        try
        {
            SmartBpPhaseRecognitionResult phaseResult;
            SmartBpCroppedFrame? phaseCrop;
            string raw;
            IReadOnlyDictionary<string, string> rawResponses;
            var messages = new List<string>
            {
                "Phase-only debug: strategy=PureOcr; no field OCR, merge, operations, or apply."
            };

            var ocr = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest([], IncludePhase: true), cancellationToken).ConfigureAwait(false);
            phaseResult = ocr.Phase;
            phaseCrop = null;
            raw = string.Join(Environment.NewLine, ocr.Regions.SelectMany(region => region.Lines.Select(line => $"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}] {line.Text}")));
            rawResponses = new Dictionary<string, string> { ["ocr_phase"] = raw };
            messages.AddRange(ocr.Diagnostics);

            var state = CreateCurrentFrameState(phaseResult.Phase);
            var gate = sceneGate.Classify(phaseResult, state, rawResponses, guidance.GetRuntimeSnapshot());
            messages.Add($"Scene: {gate.Scene}; BP recognition allowed: {gate.IsBpRecognitionAllowed}; Character operations allowed: {gate.IsCharacterOperationAllowed}; Action: {(gate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}; Reason: {gate.Reason}.");
            return new(state, phaseResult, null, phaseCrop, null, null, guidance.GetRuntimeSnapshot(),
                [], messages, new(0, 0, ["Phase-only debug: operation generation is disabled."]), raw, null, null, [], gate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(null, null, null, null, null, null, guidance.GetRuntimeSnapshot(), [], [], null, "", ex.Message);
        }
        finally
        {
            _tickGate.Release();
        }
    }

    private static SmartBpSnapshotDeltaRequest BuildFullStrategyDebugRequest(SmartBpBusinessStateRecognitionResult currentKnownState) =>
        new(
            [
                (SmartBpRecognitionRegion.RightTop, "banned_sur"),
                (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
                (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
                (SmartBpRecognitionRegion.RightBottom, "picked_hun")
            ],
            ["Full strategy debug requested phase_top and all four BP business fields."],
            currentKnownState);

    /// <summary>
    /// 解析协调器在当前 tick 中应使用的识别路径。
    /// 没有请求字段时使用仅阶段路径；请求一个或多个字段时使用字段快照路径。
    /// </summary>
    /// <param name="request">规划器构建的识别请求。</param>
    /// <returns>识别路径枚举值。</returns>
    private static SmartBpRecognitionPath ResolveRecognitionPath(SmartBpSnapshotDeltaRequest request) =>
        request.RequestedFields.Count == 0 ? SmartBpRecognitionPath.PhaseOnly : SmartBpRecognitionPath.FieldSnapshot;

    private static SmartBpDetectedOperation[] BuildCurrentFrameOperations(
        SmartBpBusinessStateRecognitionResult state,
        SmartBpCandidateOperationBuilder builder)
    {
        return new[]
        {
            builder.BuildWithDiagnostics(state, GameAction.BanSur, []).Operations,
            builder.BuildWithDiagnostics(state, GameAction.BanHun, []).Operations,
            builder.BuildWithDiagnostics(state, GameAction.PickSur, []).Operations,
            builder.BuildWithDiagnostics(state, GameAction.PickHun, []).Operations
        }.SelectMany(items => items)
         .Select(operation => operation with { ApplyMode = SmartBpDetectedOperationApplyMode.FreeSync, SourceWorkflowStepIndex = null })
         .ToArray();
    }

    private static string DescribeRecognitionTickMode(SmartBpRecognitionDebugMode debugMode) =>
        debugMode switch
        {
            SmartBpRecognitionDebugMode.FullStrategy => "full image debug",
            SmartBpRecognitionDebugMode.CurrentStageIncremental => "incremental debug",
            _ => "automatic"
        };

    private static string FormatBusinessStateForDiagnostics(SmartBpBusinessStateRecognitionResult state) =>
        JsonSerializer.Serialize(CreateCurrentKnownStateJson(state));

    private static JsonObject CreateCurrentKnownStateJson(SmartBpBusinessStateRecognitionResult? state)
    {
        static string[] Names(IEnumerable<SmartBpRecognizedCharacterSlot> slots, int count) =>
            slots.OrderBy(x => x.Index).Take(count).Select(x => string.IsNullOrWhiteSpace(x.CharacterName) ? "未选择" : x.CharacterName).ToArray();
        return state == null
            ? new JsonObject
            {
                ["banned_sur"] = new JsonArray("未选择", "未选择", "未选择", "未选择"),
                ["banned_hun"] = new JsonArray("未选择", "未选择"),
                ["picked_sur"] = new JsonArray("未选择", "未选择", "未选择", "未选择"),
                ["picked_hun"] = "未选择"
            }
            : new JsonObject
            {
                ["banned_sur"] = new JsonArray(Names(state.BannedSur, 4).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["banned_hun"] = new JsonArray(Names(state.BannedHun, 2).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["picked_sur"] = new JsonArray(Names(state.PickedSur, 4).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["picked_hun"] = string.IsNullOrWhiteSpace(state.PickedHun.CharacterName) ? "未选择" : state.PickedHun.CharacterName
            };
    }

    private static SmartBpBusinessStateRecognitionResult CreateCurrentFrameState(string phase) =>
        new()
        {
            Phase = phase,
            BannedSur = Enumerable.Range(0, 4).Select(index => new SmartBpRecognizedCharacterSlot { Index = index }).ToList(),
            BannedHun = Enumerable.Range(0, 2).Select(index => new SmartBpRecognizedCharacterSlot { Index = index }).ToList(),
            PickedSur = Enumerable.Range(0, 4).Select(index => new SmartBpRecognizedPlayerCharacterSlot { Index = index }).ToList(),
            PickedHun = new SmartBpRecognizedPlayerCharacterSlot { Index = 0 }
        };
}
