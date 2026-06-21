using System.Windows.Media.Imaging;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Abstractions;

/// <summary>Installs and validates Tesseract language data.</summary>
public interface ITesseractDataAssetManager
{
    /// <summary>Occurs when download state changes.</summary>
    event EventHandler<SmartBpDownloadState>? StateChanged;
    /// <summary>Gets the current language-data status.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validated status.</returns>
    Task<TesseractDataStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all language data assets that can be managed by SmartBP.</summary>
    /// <returns>Available Tesseract language assets.</returns>
    IReadOnlyList<TesseractLanguageAsset> GetAvailableLanguages();
    /// <summary>Installs the selected language data assets that are not already installed.</summary>
    /// <param name="languages">Language identifiers to install.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task InstallLanguagesAsync(IEnumerable<string> languages, CancellationToken cancellationToken = default);
    /// <summary>Deletes selected managed language data files from the effective tessdata directory.</summary>
    /// <param name="languages">Language identifiers to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task DeleteAsync(IEnumerable<string> languages, CancellationToken cancellationToken = default);
    /// <summary>Cancels an active download.</summary>
    void Cancel();
}

/// <summary>Reads optional NVIDIA GPU telemetry for the managed AI runtime.</summary>
public interface ISmartBpAiPerformanceMonitor
{
    /// <summary>Gets the latest GPU and llama-server process snapshot.</summary>
    /// <param name="processId">Managed llama-server process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current performance snapshot.</returns>
    Task<SmartBpAiPerformanceSnapshot> GetSnapshotAsync(int? processId, CancellationToken cancellationToken = default);
}

/// <summary>Loads bundled Qwen metadata.</summary>
public interface IQwenModelManifestProvider { /// <summary>Loads and validates the manifest.</summary>
    Task<QwenModelManifest> LoadAsync(CancellationToken cancellationToken = default); }

/// <summary>Loads bundled local vision model metadata.</summary>
public interface ILocalVisionModelManifestProvider
{
    /// <summary>Loads and validates the manifest.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The local vision model manifest.</returns>
    Task<LocalVisionModelManifest> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Loads bundled RapidOCR model metadata.</summary>
public interface IRapidOcrModelManifestProvider
{
    /// <summary>Loads and validates the RapidOCR manifest.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validated manifest.</returns>
    Task<RapidOcrModelManifest> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Installs and validates managed RapidOCR Chinese model assets.</summary>
public interface IRapidOcrModelAssetManager
{
    /// <summary>Occurs when installation state changes.</summary>
    event EventHandler<SmartBpDownloadState>? StateChanged;
    /// <summary>Gets the last calculated model status.</summary>
    RapidOcrModelStatus Status { get; }
    /// <summary>Gets the selected profile status.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected model status.</returns>
    Task<RapidOcrModelStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets available RapidOCR profiles.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Available profiles.</returns>
    Task<IReadOnlyList<RapidOcrModelProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>Checks the selected profile against RapidOCR's official online manifest.</summary>
    /// <param name="profileId">Profile id to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installed, bundled, and official version comparison.</returns>
    Task<RapidOcrModelUpdateCheckResult> CheckForUpdatesAsync(
        string profileId,
        CancellationToken cancellationToken = default);
    /// <summary>Installs one RapidOCR model profile.</summary>
    /// <param name="profileId">Profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the installation.</returns>
    Task InstallAsync(string profileId, CancellationToken cancellationToken = default);
    /// <summary>Deletes one managed RapidOCR profile.</summary>
    /// <param name="profileId">Profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing deletion.</returns>
    Task DeleteAsync(string profileId, CancellationToken cancellationToken = default);
    /// <summary>Cancels the active installation.</summary>
    void Cancel();
    /// <summary>Gets validated paths for the selected profile.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installed paths.</returns>
    Task<RapidOcrInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default);
}
/// <summary>Installs and removes Qwen model assets.</summary>
public interface IQwenModelAssetManager
{
    /// <summary>Raised when download state changes.</summary>
    event EventHandler<QwenDownloadState>? StateChanged;
    /// <summary>Gets current download state.</summary>
    QwenDownloadState State { get; }
    /// <summary>Gets the selected profile.</summary>
    Task<QwenModelProfile> GetProfileAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a specific local vision model profile.</summary>
    /// <param name="modelId">Model profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching profile.</returns>
    Task<QwenModelProfile> GetProfileAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>Gets all selectable Qwen model profiles.</summary>
    Task<IReadOnlyList<QwenModelProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>Checks installed assets, including hashes.</summary>
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    /// <summary>Checks installed assets for a specific Qwen model profile, including hashes.</summary>
    /// <param name="modelId">Qwen model profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the selected model files are installed and valid; otherwise <see langword="false"/>.</returns>
    Task<bool> IsInstalledAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>Downloads missing assets.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);
    /// <summary>Downloads missing assets for a specific Qwen model profile.</summary>
    /// <param name="modelId">Qwen model profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task InstallAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>Cancels an active download.</summary>
    void Cancel();
    /// <summary>Deletes installed assets without blocking the caller thread.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
    /// <summary>Deletes installed assets for a specific Qwen model profile without blocking the caller thread.</summary>
    /// <param name="modelId">Qwen model profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task DeleteAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>Gets installed model and projector paths.</summary>
    Task<QwenInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets installed model and projector paths for a specific local vision model profile.</summary>
    /// <param name="modelId">Model profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validated installed paths.</returns>
    Task<QwenInstalledPaths> GetInstalledPathsAsync(string modelId, CancellationToken cancellationToken = default);
}

/// <summary>Installs and removes managed local vision model assets.</summary>
public interface ILocalVisionModelAssetManager : IQwenModelAssetManager;
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
    /// <summary>Checks whether a specific runtime asset is installed without loading the manifest.</summary>
    /// <param name="assetId">The asset identifier.</param>
    /// <param name="entryExe">The entry executable filename.</param>
    Task<bool> IsAssetInstalledAsync(string assetId, string entryExe, CancellationToken cancellationToken = default);
    /// <summary>Installs the selected runtime.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);
    /// <summary>Cancels an active installation.</summary>
    void Cancel();
    /// <summary>Deletes the selected installed runtime without blocking the caller thread.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets the selected installed server executable.</summary>
    Task<string> GetInstalledExecutablePathAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets whether a previous runtime snapshot can be restored.</summary>
    Task<bool> CanRollbackAsync(CancellationToken cancellationToken = default);
    /// <summary>Swaps the current and previous runtime snapshots.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
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
    /// <summary>Gets the server role.</summary>
    LlamaVisionServerRole Role { get; }
    /// <summary>Gets the configured server port.</summary>
    int Port { get; }
    /// <summary>Gets a display status.</summary>
    string Status { get; }
    /// <summary>Gets the managed llama-server process identifier.</summary>
    int? ProcessId { get; }
    /// <summary>Starts and awaits readiness.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops the managed process.</summary>
    Task StopAsync();
    /// <summary>Force-stops the previously recorded managed llama-server process, if it is still alive.</summary>
    Task ForceStopManagedProcessAsync(CancellationToken cancellationToken = default);
}

/// <summary>Resolves managed llama.cpp server managers by role.</summary>
public interface ILlamaCppServerManagerFactory
{
    /// <summary>Gets the manager for a server role.</summary>
    /// <param name="role">Server role.</param>
    /// <returns>The role-specific server manager.</returns>
    ILlamaCppServerManager Get(LlamaVisionServerRole role);
}

/// <summary>Classifies the scene and phase without field extraction.</summary>
public interface ISmartBpScenePhaseController
{
    /// <summary>Recognizes scene and phase from a frame.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scene and phase decision.</returns>
    Task<SmartBpScenePhaseDecision> RecognizeAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}

/// <summary>Extracts a raw text transcript using an AI OCR model.</summary>
public interface ISmartBpAiOcrTranscriptRecognitionService
{
    /// <summary>Recognizes visible text lines from requested business regions.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="regions">Requested regions and owning field names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw AI OCR transcript.</returns>
    Task<SmartBpAiOcrTranscriptResult> RecognizeAsync(
        BitmapSource frame,
        IReadOnlyList<(SmartBpRecognitionRegion Region, string Field)> regions,
        CancellationToken cancellationToken = default);
}

/// <summary>Interprets raw AI OCR text transcripts into SmartBP field updates without using OCR coordinates.</summary>
public interface ISmartBpAiOcrTranscriptInterpreter
{
    /// <summary>Maps one AI OCR transcript to a business field update.</summary>
    /// <param name="transcript">Raw transcript result.</param>
    /// <param name="region">Source coarse BP region.</param>
    /// <param name="field">Target business field id.</param>
    /// <returns>The interpreted field update and diagnostics.</returns>
    (SmartBpSnapshotFieldUpdate Update, IReadOnlyList<string> Diagnostics) Interpret(
        SmartBpAiOcrTranscriptResult transcript,
        SmartBpRecognitionRegion region,
        string field);
}

/// <summary>Uses the Business AI model to fuse OCR or AI OCR evidence into structured SmartBP field updates.</summary>
public interface ISmartBpBusinessAiFusionService
{
    /// <summary>Converts transcript evidence into requested SmartBP field updates.</summary>
    /// <param name="phase">Recognized phase and scene evidence.</param>
    /// <param name="evidence">Transcript evidence grouped by BP region.</param>
    /// <param name="requestedFields">Business fields that may be updated.</param>
    /// <param name="currentKnownState">Current locally known BP state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured snapshot delta returned by the Business AI model.</returns>
    Task<(SmartBpSnapshotDeltaResult Delta, string RawJson, IReadOnlyList<string> Diagnostics)> FuseAsync(
        SmartBpPhaseRecognitionResult phase,
        IReadOnlyList<SmartBpAiOcrTranscriptRegionEvidence> evidence,
        IReadOnlyCollection<string> requestedFields,
        SmartBpBusinessStateRecognitionResult currentKnownState,
        CancellationToken cancellationToken = default);
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
    /// <param name="provider">Optional OCR provider name.</param>
    /// <returns>Resolved character information, or unresolved details.</returns>
    SmartBpNormalizedCharacter ResolveCharacterFromLine(string text, Core.Enums.Camp camp, int slotIndex, string? provider = null);
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
/// <summary>Recognizes the BP phase and individual field snapshots using independent AI requests.</summary>
public interface ISmartBpAiFieldSnapshotRecognitionService
{
    /// <summary>Recognizes only the phase crop without any business field updates.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The phase-only recognition result.</returns>
    Task<SmartBpAiPhaseOnlyResult> RecognizePhaseOnlyAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>Recognizes the current visible snapshot of one business field from its crop.</summary>
    /// <param name="frame">Source frame.</param>
    /// <param name="region">Coarse crop region that owns the field.</param>
    /// <param name="field">Business field id (banned_sur, banned_hun, picked_sur, picked_hun).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The field snapshot recognition result.</returns>
    Task<SmartBpAiFieldSnapshotResult> RecognizeFieldAsync(
        BitmapSource frame,
        SmartBpRecognitionRegion region,
        string field,
        CancellationToken cancellationToken = default);
}
/// <summary>Stores the locally merged incremental SmartBP recognition state.</summary>
public interface ISmartBpRecognitionStateStore
{
    /// <summary>Gets a complete business-state snapshot.</summary>
    SmartBpBusinessStateRecognitionResult Snapshot { get; }
    /// <summary>Applies one model delta to the local state.</summary>
    IReadOnlyList<string> ApplyDelta(SmartBpSnapshotDeltaResult delta, long frameSequence, DateTimeOffset timestamp);
    /// <summary>Applies one field snapshot to the local state using per-slot merge rules.</summary>
    /// <param name="field">Business field id.</param>
    /// <param name="snapshot">Field snapshot update carrying slot_state evidence.</param>
    /// <param name="frameSequence">Frame sequence number.</param>
    /// <param name="timestamp">Application timestamp.</param>
    /// <returns>Per-slot merge diagnostics.</returns>
    IReadOnlyList<string> ApplyFieldSnapshot(string field, SmartBpSnapshotFieldUpdate snapshot, long frameSequence, DateTimeOffset timestamp);
    /// <summary>Updates only the locally merged phase.</summary>
    /// <param name="phase">Recognized phase.</param>
    /// <param name="frameSequence">Frame sequence number.</param>
    void ApplyPhase(string phase, long frameSequence);
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
    /// <summary>Gets metrics from the most recently completed response.</summary>
    LlamaCppResponseMetrics? LastResponseMetrics { get; }
    /// <summary>Gets the finish reason from the most recently completed response.</summary>
    string? LastFinishReason { get; }
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
    /// <summary>Recognizes one field snapshot from a single cropped image using a field-specific prompt and schema.</summary>
    /// <param name="imageDataUrl">Encoded crop image data URL.</param>
    /// <param name="field">Business field id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw model JSON response (already repaired when using prompt-and-repair mode).</returns>
    Task<string> RecognizeFieldSnapshotAsync(string imageDataUrl, string field, CancellationToken cancellationToken = default);
    /// <summary>Fuses text-only transcript evidence into a snapshot delta through the Business AI server.</summary>
    /// <param name="prompt">Text-only business fusion prompt.</param>
    /// <param name="lockedPhase">Authoritative phase that the fusion response must preserve.</param>
    /// <param name="requestedFields">Fields allowed in the output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw model JSON response, repaired when needed.</returns>
    Task<string> FuseTranscriptEvidenceAsync(string prompt, string lockedPhase, IReadOnlyCollection<string> requestedFields, CancellationToken cancellationToken = default);
    /// <summary>Recognizes only the phase crop using the short phase-only prompt.</summary>
    /// <param name="imageDataUrl">Encoded phase crop image data URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw model JSON response (already repaired when using prompt-and-repair mode).</returns>
    Task<string> RecognizePhaseOnlyAsync(string imageDataUrl, CancellationToken cancellationToken = default);
}

/// <summary>Classifies the current Identity V scene and gates BP writes.</summary>
public interface ISmartBpSceneGateService
{
    /// <summary>Classifies scene evidence without mutating game state.</summary>
    SmartBpSceneGateResult Classify(
        SmartBpPhaseRecognitionResult phase,
        SmartBpBusinessStateRecognitionResult state,
        IReadOnlyDictionary<string, string> rawResponses,
        Core.Models.GameGuidanceRuntimeSnapshot guidanceSnapshot);
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
    /// <summary>Writes one diagnostic line. No-op when <see cref="IsEnabled"/> is false.</summary>
    /// <param name="source">Short subsystem name.</param>
    /// <param name="message">Diagnostic message.</param>
    void Write(string source, string message);
    /// <summary>When false, <see cref="Write"/> is a no-op and no events are raised.</summary>
    bool IsEnabled { get; set; }
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
    /// <summary>Runs one stage-aware recognition tick without applying deltas, character operations, or guidance synchronization.</summary>
    Task<SmartBpAutoRecognitionTickResult> RunOneTickDryRunAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>Runs the selected recognition strategy with all BP business fields requested for debugging.</summary>
    /// <param name="frame">Frame to recognize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full strategy recognition result.</returns>
    Task<SmartBpAutoRecognitionTickResult> RunFullRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>Runs the selected strategy with the automatic planner request shape without applying operations or guidance changes.</summary>
    /// <param name="frame">Frame to recognize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The incremental strategy preview result.</returns>
    Task<SmartBpAutoRecognitionTickResult> RunIncrementalRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>Runs only phase and scene recognition for debugging.</summary>
    /// <param name="frame">Frame to recognize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The phase-only recognition result.</returns>
    Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}

/// <summary>Owns one automatic step-commit transaction.</summary>
public interface ISmartBpStepCommitScheduler
{
    /// <summary>Processes one frame through recognition, apply, and optional guidance synchronization.</summary>
    Task<SmartBpStepCommitResult> ProcessTickAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}
