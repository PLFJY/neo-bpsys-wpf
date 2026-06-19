using System.Windows.Media.Imaging;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Abstractions;

/// <summary>Loads bundled Qwen metadata.</summary>
public interface IQwenModelManifestProvider { /// <summary>Loads and validates the manifest.</summary>
    Task<QwenModelManifest> LoadAsync(CancellationToken cancellationToken = default); }
/// <summary>Installs and removes Qwen model assets.</summary>
public interface IQwenModelAssetManager
{
    /// <summary>Raised when download state changes.</summary>
    event EventHandler<QwenDownloadState>? StateChanged;
    /// <summary>Gets current download state.</summary>
    QwenDownloadState State { get; }
    /// <summary>Gets the selected profile.</summary>
    Task<QwenModelProfile> GetProfileAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all selectable Qwen model profiles.</summary>
    Task<IReadOnlyList<QwenModelProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>Checks installed assets, including hashes.</summary>
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    /// <summary>Downloads missing assets.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);
    /// <summary>Cancels an active download.</summary>
    void Cancel();
    /// <summary>Deletes installed assets without blocking the caller thread.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets installed model and projector paths.</summary>
    Task<(string ModelPath, string MmprojPath)> GetInstalledPathsAsync(CancellationToken cancellationToken = default);
}
/// <summary>Persists recognition settings.</summary>
public interface ISmartBpRecognitionSettingsService
{
    /// <summary>Gets current settings.</summary>
    SmartBpRecognitionSettings Settings { get; }
    /// <summary>Saves current settings.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
/// <summary>Loads bundled SmartBP recognition prompt profiles.</summary>
public interface ISmartBpPromptProfileProvider
{
    /// <summary>Gets available bundled profiles.</summary>
    Task<IReadOnlyList<SmartBpPromptProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>Loads one profile by id.</summary>
    Task<SmartBpPromptProfile> LoadAsync(string profileId, CancellationToken cancellationToken = default);
}
/// <summary>Loads llama.cpp runtime metadata.</summary>
public interface ILlamaCppRuntimeManifestProvider
{
    /// <summary>Loads and validates the bundled runtime manifest.</summary>
    Task<LlamaCppRuntimeManifest> LoadAsync(CancellationToken cancellationToken = default);
}
/// <summary>Installs and manages a selected llama.cpp runtime.</summary>
public interface ILlamaCppRuntimeAssetManager
{
    /// <summary>Raised when installation state changes.</summary>
    event EventHandler<LlamaCppRuntimeInstallState>? StateChanged;
    /// <summary>Gets current installation state.</summary>
    LlamaCppRuntimeInstallState State { get; }
    /// <summary>Gets selectable runtime assets.</summary>
    Task<IReadOnlyList<LlamaCppRuntimeAsset>> GetAvailableAssetsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets the selected runtime asset.</summary>
    Task<LlamaCppRuntimeAsset> GetSelectedAssetAsync(CancellationToken cancellationToken = default);
    /// <summary>Checks whether the selected runtime is installed.</summary>
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    /// <summary>Installs the selected runtime.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);
    /// <summary>Cancels an active installation.</summary>
    void Cancel();
    /// <summary>Deletes the selected installed runtime without blocking the caller thread.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets the selected installed server executable.</summary>
    Task<string> GetInstalledExecutablePathAsync(CancellationToken cancellationToken = default);
}
/// <summary>Checks for remote llama.cpp runtime manifest updates.</summary>
public interface ILlamaCppRuntimeUpdateService
{
    /// <summary>Checks whether a newer runtime manifest is available.</summary>
    Task<LlamaCppRuntimeUpdateCheckResult> CheckForUpdatesAsync(bool force, CancellationToken cancellationToken = default);
}
/// <summary>Controls the local llama.cpp server.</summary>
public interface ILlamaCppServerManager
{
    /// <summary>Gets whether the managed process is ready.</summary>
    bool IsRunning { get; }
    /// <summary>Gets a display status.</summary>
    string Status { get; }
    /// <summary>Starts and awaits readiness.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops the managed process.</summary>
    Task StopAsync();
    /// <summary>Force-stops the previously recorded managed llama-server process, if it is still alive.</summary>
    Task ForceStopManagedProcessAsync(CancellationToken cancellationToken = default);
}
/// <summary>Encodes WPF frames for multimodal requests.</summary>
public interface ISmartBpImageEncoder { /// <summary>Encodes a PNG data URL.</summary>
    string EncodeDataUrl(BitmapSource source, int maxWidth); }
/// <summary>Loads and persists SmartBP coarse recognition crop layout profiles.</summary>
public interface ISmartBpRecognitionRegionProfileService
{
    /// <summary>Loads the user override profile, or the bundled default profile when no override exists.</summary>
    Task<SmartBpRecognitionLayoutProfile> LoadAsync(CancellationToken cancellationToken = default);
    /// <summary>Saves a user override profile.</summary>
    Task SaveUserOverrideAsync(SmartBpRecognitionLayoutProfile profile, CancellationToken cancellationToken = default);
    /// <summary>Deletes the user override so the bundled default profile is used again.</summary>
    Task ResetUserOverrideAsync(CancellationToken cancellationToken = default);
}
/// <summary>Crops SmartBP recognition frames into coarse BP regions.</summary>
public interface ISmartBpRecognitionFrameCropper
{
    /// <summary>Crops a frame to the requested coarse region and returns diagnostics.</summary>
    SmartBpCroppedFrame CropWithInfo(BitmapSource source, SmartBpRecognitionRegion region);
    /// <summary>Crops a frame to the requested coarse region.</summary>
    BitmapSource Crop(BitmapSource source, SmartBpRecognitionRegion region);
}
/// <summary>Keeps recent frames for transition finalization.</summary>
public interface ISmartBpFrameRingBuffer
{
    /// <summary>Adds one captured frame.</summary>
    void AddFrame(long sequence, BitmapSource frame, DateTimeOffset timestamp);
    /// <summary>Gets recent frames within a time window.</summary>
    IReadOnlyList<SmartBpBufferedFrame> GetRecentFrames(TimeSpan window);
    /// <summary>Gets the best recent frame for a region.</summary>
    SmartBpBufferedFrame? GetBestFrameForRegion(SmartBpRecognitionRegion region, TimeSpan lookBehind);
}
/// <summary>Detects whether a cropped recognition region changed enough to refresh.</summary>
public interface ISmartBpCropChangeDetector
{
    /// <summary>Analyzes one crop and returns a lightweight change result.</summary>
    SmartBpCropChangeResult Analyze(SmartBpRecognitionRegion region, BitmapSource crop, long sequence);
}
/// <summary>Builds a single OCR contact sheet from multiple SmartBP recognition regions.</summary>
public interface ISmartBpOcrContactSheetBuilder
{
    /// <summary>Builds an unlabeled contact sheet and coordinate mapping.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="regions">Requested regions.</param>
    /// <returns>Contact-sheet image and mappings.</returns>
    SmartBpOcrContactSheet Build(BitmapSource frame, IReadOnlyList<SmartBpRecognitionRegion> regions);
}
/// <summary>Resolves OCR text lines to canonical character names.</summary>
public interface ISmartBpOcrTextResolver
{
    /// <summary>Resolves one OCR line as a candidate character.</summary>
    /// <param name="text">OCR text.</param>
    /// <param name="camp">Target camp.</param>
    /// <param name="slotIndex">Visual slot index.</param>
    /// <returns>Resolved character information, or unresolved details.</returns>
    SmartBpNormalizedCharacter ResolveCharacterFromLine(string text, Core.Enums.Camp camp, int slotIndex);
}
/// <summary>Recognizes BP state from PaddleOCR text and bounding boxes.</summary>
public interface ISmartBpOcrBpRecognitionService
{
    /// <summary>Runs one OCR BP recognition pass.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="request">Requested OCR regions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OCR recognition result.</returns>
    Task<SmartBpOcrRecognitionResult> RecognizeAsync(
        BitmapSource frame,
        SmartBpOcrRecognitionRequest request,
        CancellationToken cancellationToken = default);
}
/// <summary>Recognizes one incremental OCR snapshot delta.</summary>
public interface ISmartBpOcrSnapshotDeltaRecognitionService
{
    /// <summary>Recognizes a requested OCR delta package from one frame.</summary>
    Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default);
}
/// <summary>Recognizes and locally merges phase plus all four coarse BP content regions.</summary>
public interface ISmartBpRegionSnapshotRecognitionService
{
    /// <summary>Recognizes one region-gated BP snapshot.</summary>
    Task<SmartBpRegionSnapshot> RecognizeSnapshotAsync(BitmapSource frame, SmartBpRegionSnapshotRecognitionMode mode, CancellationToken cancellationToken = default);
}
/// <summary>Recognizes one incremental multi-region SmartBP snapshot delta.</summary>
public interface ISmartBpSnapshotDeltaRecognitionService
{
    /// <summary>Recognizes a requested delta package from one frame.</summary>
    Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default);
}
/// <summary>Stores the locally merged incremental SmartBP recognition state.</summary>
public interface ISmartBpRecognitionStateStore
{
    /// <summary>Gets a complete business-state snapshot.</summary>
    SmartBpBusinessStateRecognitionResult Snapshot { get; }
    /// <summary>Applies one model delta to the local state.</summary>
    IReadOnlyList<string> ApplyDelta(SmartBpSnapshotDeltaResult delta, long frameSequence, DateTimeOffset timestamp);
    /// <summary>Returns field staleness diagnostics.</summary>
    IReadOnlyList<string> GetStaleFieldDiagnostics(DateTimeOffset timestamp, int staleMilliseconds);
    /// <summary>Resets all locally merged state.</summary>
    void Reset();
}
/// <summary>Plans which cropped regions should be refreshed on the next incremental request.</summary>
public interface ISmartBpSnapshotRecognitionPlanner
{
    /// <summary>Builds the next recognition request package.</summary>
    SmartBpSnapshotDeltaRequest BuildRequest(Core.Models.GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpBusinessStateRecognitionResult currentLocalSnapshot,
        SmartBpRecognitionLedgerSnapshot ledgerSnapshot);
}
/// <summary>Merges independent content-region outputs into the simplified BP business state.</summary>
public interface ISmartBpBusinessStateMerger
{
    /// <summary>Merges the authoritative phase and four optional region results.</summary>
    SmartBpBusinessStateRecognitionResult Merge(SmartBpPhaseRecognitionResult phase,
        SmartBpFocusedBusinessExtractionResult? bannedSur,
        SmartBpFocusedBusinessExtractionResult? bannedHun,
        SmartBpFocusedBusinessExtractionResult? pickedSur,
        SmartBpFocusedBusinessExtractionResult? pickedHun);
}
/// <summary>Sends independent OpenAI-compatible requests.</summary>
public interface ILlamaCppOpenAiClient
{
    /// <summary>Recognizes one image using the manual generic schema.</summary>
    Task<string> RecognizeAsync(string imageDataUrl, SmartBpRecognitionTask task, CancellationToken cancellationToken = default);
    /// <summary>Recognizes the BP phase from a top-operation crop.</summary>
    Task<string> RecognizePhaseAsync(string imageDataUrl, CancellationToken cancellationToken = default);
    /// <summary>Extracts business content from a focused coarse-region crop.</summary>
    Task<string> RecognizeFocusedBusinessAsync(string imageDataUrl, Core.Enums.GameAction action, CancellationToken cancellationToken = default);
    /// <summary>Detects the active BP stage without extracting characters.</summary>
    Task<string> DetectStageAsync(string imageDataUrl, CancellationToken cancellationToken = default);
    /// <summary>Extracts the operation for a locally selected guidance step.</summary>
    Task<string> RecognizeFocusedAsync(string imageDataUrl, Core.Enums.GameAction action, IReadOnlyList<int> indexes, CancellationToken cancellationToken = default);
    /// <summary>Recognizes a phase plus requested content updates from multiple cropped images in one request.</summary>
    Task<string> RecognizeSnapshotDeltaAsync(IReadOnlyList<SmartBpMultimodalRegionInput> regions, SmartBpSnapshotDeltaRequest request, CancellationToken cancellationToken = default);
}
/// <summary>Resolves model names against shared character dictionaries.</summary>
public interface ISmartBpCharacterResolver { /// <summary>Resolves a character name safely.</summary>
    SmartBpNormalizedCharacter Resolve(string? rawName, Core.Enums.Camp camp, int slot, double confidence); }
/// <summary>Runs and normalizes one recognition request.</summary>
public interface ISmartBpAiRecognitionService { /// <summary>Recognizes one frame.</summary>
    Task<SmartBpRecognitionPreview> RecognizeAsync(BitmapSource frame, SmartBpRecognitionTask task, CancellationToken cancellationToken = default); }

/// <summary>Publishes bounded, user-visible diagnostics for the SmartBP AI pipeline.</summary>
public interface ISmartBpDebugLog
{
    /// <summary>Raised whenever a diagnostic line is written.</summary>
    event EventHandler<SmartBpDebugMessageEventArgs>? MessageWritten;
    /// <summary>Writes one diagnostic line.</summary>
    /// <param name="source">Short subsystem name.</param>
    /// <param name="message">Diagnostic message.</param>
    void Write(string source, string message);
}

/// <summary>Reconciles model stage output with the authoritative GameGuidance workflow.</summary>
public interface ISmartBpGuidanceSyncService
{
    /// <summary>Synchronizes to the current or nearest compatible future step.</summary>
    Task<SmartBpGuidanceSyncResult> SyncAsync(SmartBpBusinessStateRecognitionResult businessState, CancellationToken cancellationToken = default);
}

/// <summary>Applies locally validated candidate operations through character selection services.</summary>
public interface ISmartBpDetectedOperationApplier
{
    /// <summary>Applies accepted resolved operations.</summary>
    Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default);
}

/// <summary>Builds ordered workflow backfill candidates from a complete merged BP snapshot.</summary>
public interface ISmartBpWorkflowBackfillService
{
    /// <summary>Builds a plan from the current workflow without mutating guidance state.</summary>
    SmartBpWorkflowBackfillPlan BuildPlan(SmartBpBusinessStateRecognitionResult snapshot, Core.Models.GameGuidanceRuntimeSnapshot guidanceSnapshot);
}

/// <summary>Tracks successfully completed workflow operations for the current game progress.</summary>
public interface ISmartBpRecognitionLedger
{
    /// <summary>Returns whether the operation was already completed.</summary>
    bool IsStepOperationCompleted(SmartBpWorkflowOperationKey key);
    /// <summary>Marks an operation completed after apply or a confirmed no-op.</summary>
    void MarkCompleted(SmartBpWorkflowOperationKey key);
    /// <summary>Records a non-terminal skip reason without marking the operation completed.</summary>
    void MarkSkipped(SmartBpWorkflowOperationKey key, string reason);
    /// <summary>Clears all recognition state for the current game.</summary>
    void ResetForCurrentGame();
    /// <summary>Returns a read-only snapshot for planning.</summary>
    SmartBpRecognitionLedgerSnapshot GetSnapshot();
}

/// <summary>Coordinates stage detection, guidance reconciliation and focused extraction.</summary>
public interface ISmartBpAutoRecognitionCoordinator
{
    /// <summary>Gets whether automatic mode is running.</summary>
    bool IsRunning { get; }
    /// <summary>Starts automatic mode.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops automatic mode.</summary>
    Task StopAsync();
    /// <summary>Runs one stage-aware automatic recognition tick.</summary>
    Task<SmartBpAutoRecognitionTickResult> RunOneTickAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}

/// <summary>Owns one automatic step-commit transaction.</summary>
public interface ISmartBpStepCommitScheduler
{
    /// <summary>Processes one frame through recognition, apply, and optional guidance synchronization.</summary>
    Task<SmartBpStepCommitResult> ProcessTickAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}
