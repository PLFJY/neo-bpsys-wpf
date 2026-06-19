using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

/// <summary>Supported AI recognition tasks.</summary>
public enum SmartBpRecognitionTask { DetectStage, BanSur, BanHun, PickSur, PickHun, CharacterDistribution, FullBpScan }

/// <summary>Coarse SmartBP recognition crop regions.</summary>
public enum SmartBpRecognitionRegion { PhaseTop, LeftTop, RightTop, LeftBottom, RightBottom }

/// <summary>Controls how many BP content regions are recognized for one snapshot.</summary>
public enum SmartBpRegionSnapshotRecognitionMode { FullAllRegions, PendingAndCurrentRegions }

/// <summary>Normalized recognition crop rectangle.</summary>
public sealed class SmartBpRecognitionRegionRect
{
    /// <summary>Gets or sets normalized left coordinate.</summary>
    public double X { get; set; }
    /// <summary>Gets or sets normalized top coordinate.</summary>
    public double Y { get; set; }
    /// <summary>Gets or sets normalized width.</summary>
    public double Width { get; set; }
    /// <summary>Gets or sets normalized height.</summary>
    public double Height { get; set; }
}

/// <summary>SmartBP coarse recognition layout profile.</summary>
public sealed class SmartBpRecognitionLayoutProfile
{
    /// <summary>Gets or sets schema version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Gets or sets profile id.</summary>
    public string Id { get; set; } = "idv-default-16x9";
    /// <summary>Gets or sets base aspect ratio label.</summary>
    public string BaseAspectRatio { get; set; } = "16:9";
    /// <summary>Gets or sets normalized regions by json id.</summary>
    public Dictionary<string, SmartBpRecognitionRegionRect> Regions { get; set; } = [];
}

/// <summary>One cropped recognition frame with diagnostics.</summary>
public sealed record SmartBpCroppedFrame(
    SmartBpRecognitionRegion Region,
    BitmapSource Image,
    int X,
    int Y,
    int Width,
    int Height)
{
    /// <summary>Gets a compact pixel rectangle description.</summary>
    public string PixelRectText => $"x={X}, y={Y}, width={Width}, height={Height}";
}

/// <summary>Phase-only model output for region-gated recognition.</summary>
public sealed class SmartBpPhaseRecognitionResult
{
    /// <summary>Gets or sets the detected Chinese BP phase.</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
}

/// <summary>Qwen manifest root.</summary>
public sealed class QwenModelManifest
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Gets or sets model profiles.</summary>
    public List<QwenModelProfile> Models { get; set; } = [];
}

/// <summary>One Qwen model and its matching vision projector.</summary>
public sealed class QwenModelProfile
{
    /// <summary>Gets or sets the profile id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gets or sets the model URL.</summary>
    public string ModelUrl { get; set; } = "";
    /// <summary>Gets or sets the model filename.</summary>
    public string ModelFileName { get; set; } = "";
    /// <summary>Gets or sets the projector URL.</summary>
    public string MmprojUrl { get; set; } = "";
    /// <summary>Gets or sets the projector filename.</summary>
    public string MmprojFileName { get; set; } = "";
    /// <summary>Gets or sets the optional model hash.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Gets or sets the optional projector hash.</summary>
    public string? MmprojSha256 { get; set; }
}

/// <summary>Persisted AI recognition settings.</summary>
public sealed class SmartBpRecognitionSettings
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Gets or sets llama-server path.</summary>
    public string LlamaServerExecutablePath { get; set; } = "";
    /// <summary>Gets or sets the loopback port.</summary>
    public int LlamaServerPort { get; set; } = 18080;
    /// <summary>Gets or sets the llama.cpp context size.</summary>
    public int LlamaContextSize { get; set; } = 8192;
    /// <summary>Gets or sets selected Qwen profile.</summary>
    public string SelectedQwenModelId { get; set; } = "qwen3.5-2b-q4km";
    /// <summary>Gets or sets the selected projector profile label.</summary>
    public string SelectedMmprojId { get; set; } = "mmproj-f16";
    /// <summary>Gets or sets the bundled prompt profile id.</summary>
    public string PromptProfileId { get; set; } = "zh-CN";
    /// <summary>Gets or sets the managed llama.cpp runtime asset id.</summary>
    public string SelectedLlamaRuntimeId { get; set; } = "";
    /// <summary>Gets or sets maximum encoded width.</summary>
    public int MaxImageWidth { get; set; } = 1280;
    /// <summary>Gets or sets image encoding format.</summary>
    public string ImageFormat { get; set; } = "png";
    /// <summary>Gets or sets inference temperature.</summary>
    public double Temperature { get; set; }
    /// <summary>Gets or sets focused token limit.</summary>
    public int FocusedMaxTokens { get; set; } = 1024;
    /// <summary>Gets or sets full-scan token limit.</summary>
    public int FullScanMaxTokens { get; set; } = 2048;
    /// <summary>Gets or sets loop interval.</summary>
    public int RecognitionIntervalMs { get; set; } = 1200;
    /// <summary>Gets or sets minimum recommended interval.</summary>
    public int MinRecognitionIntervalMs { get; set; } = 500;
    /// <summary>Gets or sets maximum recommended interval.</summary>
    public int MaxRecognitionIntervalMs { get; set; } = 5000;
    /// <summary>Gets or sets required stable preview frames.</summary>
    public int RequiredStableFrames { get; set; } = 2;
    /// <summary>Gets or sets cooldown after recognition.</summary>
    public int PostRecognitionCooldownMs { get; set; } = 1200;
    /// <summary>Gets or sets whether busy frames are dropped.</summary>
    public bool DropFrameWhenBusy { get; set; } = true;
    /// <summary>Gets or sets process priority.</summary>
    public string ProcessPriority { get; set; } = "BelowNormal";
    /// <summary>Gets or sets CPU thread count.</summary>
    public int CpuThreads { get; set; } = 2;
    /// <summary>Gets or sets whether automatic mode may synchronize GameGuidance.</summary>
    public bool EnableAutoGuidanceSync { get; set; }
    /// <summary>Gets or sets whether accepted operations may be applied.</summary>
    public bool EnableAutoApplyRecognition { get; set; }
    /// <summary>Gets or sets minimum stage confidence.</summary>
    public double StageConfidenceThreshold { get; set; } = 0.80;
    /// <summary>Gets or sets guidance reconciliation lookahead.</summary>
    public int GuidanceSyncLookAheadSteps { get; set; } = 4;
    /// <summary>Gets or sets whether late workflow backfill should replay frontend animations.</summary>
    public bool PlayBackfillAnimations { get; set; }
    /// <summary>Gets or sets the number of matching snapshots required before automatic apply.</summary>
    public int RequiredStableSnapshots { get; set; } = 1;
    /// <summary>Gets or sets whether automatic recognition should use one multi-image snapshot delta request.</summary>
    public bool UseMultiImageSnapshotRequest { get; set; } = true;
    /// <summary>Gets or sets how many previous workflow steps are considered when planning content-region refreshes.</summary>
    public int RecognitionBackfillLookBehindSteps { get; set; } = 2;
    /// <summary>Gets or sets how long a locally merged recognition field may remain fresh.</summary>
    public int RecognitionFieldStaleMilliseconds { get; set; } = 2500;
    /// <summary>Gets or sets an optional delay before applying current-step animated operations.</summary>
    public int RecognitionVisualBufferMilliseconds { get; set; }
    /// <summary>Gets or sets llama.cpp parallel slot count.</summary>
    public int LlamaParallelSlots { get; set; } = 1;
    /// <summary>Gets or sets llama.cpp GPU layer count; -1 means auto.</summary>
    public int LlamaGpuLayers { get; set; } = -1;
    /// <summary>Gets or sets whether llama.cpp flash attention is enabled.</summary>
    public bool LlamaFlashAttention { get; set; } = true;
    /// <summary>Gets or sets llama.cpp batch size.</summary>
    public int LlamaBatchSize { get; set; } = 512;
    /// <summary>Gets or sets llama.cpp micro-batch size.</summary>
    public int LlamaUBatchSize { get; set; } = 512;
    /// <summary>Gets or sets whether stale managed llama-server processes may be killed automatically.</summary>
    public bool AutoKillStaleManagedLlamaServer { get; set; } = true;
}

/// <summary>Model-facing BP business-state recognition result.</summary>
public sealed class SmartBpBusinessStateRecognitionResult
{
    /// <summary>Gets or sets the detected BP phase.</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
    /// <summary>Gets or sets survivor ban slots.</summary>
    [JsonPropertyName("banned_sur")] public List<SmartBpRecognizedCharacterSlot> BannedSur { get; set; } = [];
    /// <summary>Gets or sets hunter ban slots.</summary>
    [JsonPropertyName("banned_hun")] public List<SmartBpRecognizedCharacterSlot> BannedHun { get; set; } = [];
    /// <summary>Gets or sets survivor pick or distribution slots.</summary>
    [JsonPropertyName("picked_sur")] public List<SmartBpRecognizedPlayerCharacterSlot> PickedSur { get; set; } = [];
    /// <summary>Gets or sets hunter pick slot.</summary>
    [JsonPropertyName("picked_hun")] public SmartBpRecognizedPlayerCharacterSlot PickedHun { get; set; } = new();
}

/// <summary>One recognized character slot.</summary>
public class SmartBpRecognizedCharacterSlot
{
    /// <summary>Gets or sets visual slot index.</summary>
    [JsonPropertyName("index")] public int Index { get; set; }
    /// <summary>Gets or sets raw model character name or 未选择.</summary>
    [JsonPropertyName("character_name")] public string CharacterName { get; set; } = "未选择";
}

/// <summary>One recognized player-bound character slot.</summary>
public sealed class SmartBpRecognizedPlayerCharacterSlot : SmartBpRecognizedCharacterSlot
{
    /// <summary>Gets or sets visible player ID, if any.</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
}

/// <summary>Focused model output for one cropped business region.</summary>
public sealed class SmartBpFocusedBusinessExtractionResult
{
    /// <summary>Gets or sets the phase that selected this focused region.</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
    /// <summary>Gets or sets the target business field.</summary>
    [JsonPropertyName("target_field")] public string TargetField { get; set; } = "";
    /// <summary>Gets or sets focused slots for ban or survivor-pick regions.</summary>
    [JsonPropertyName("slots")] public List<SmartBpRecognizedPlayerCharacterSlot> Slots { get; set; } = [];
    /// <summary>Gets or sets the focused hunter pick slot.</summary>
    [JsonPropertyName("picked_hun")] public SmartBpRecognizedPlayerCharacterSlot? PickedHun { get; set; }
}

/// <summary>One locally merged BP snapshot produced from the phase crop and four content crops.</summary>
public sealed class SmartBpRegionSnapshot
{
    /// <summary>Gets the authoritative phase recognition result.</summary>
    public SmartBpPhaseRecognitionResult Phase { get; init; } = new();
    /// <summary>Gets the upper-right survivor-ban extraction.</summary>
    public SmartBpFocusedBusinessExtractionResult? BannedSurRegion { get; init; }
    /// <summary>Gets the upper-left hunter-ban extraction.</summary>
    public SmartBpFocusedBusinessExtractionResult? BannedHunRegion { get; init; }
    /// <summary>Gets the lower-left survivor-pick extraction.</summary>
    public SmartBpFocusedBusinessExtractionResult? PickedSurRegion { get; init; }
    /// <summary>Gets the lower-right hunter-pick extraction.</summary>
    public SmartBpFocusedBusinessExtractionResult? PickedHunRegion { get; init; }
    /// <summary>Gets the merged simplified business state.</summary>
    public SmartBpBusinessStateRecognitionResult BusinessState { get; init; } = new();
    /// <summary>Gets all crop diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    /// <summary>Gets the phase crop used by the model.</summary>
    public SmartBpCroppedFrame? PhaseCrop { get; init; }
    /// <summary>Gets all four content crops used by the model.</summary>
    public IReadOnlyList<SmartBpCroppedFrame> ContentCrops { get; init; } = [];
    /// <summary>Gets raw model responses keyed by logical region.</summary>
    public IReadOnlyDictionary<string, string> RawResponses { get; init; } = new Dictionary<string, string>();
}

/// <summary>One cropped region image included in a multi-image snapshot request.</summary>
public sealed record SmartBpMultimodalRegionInput(string Id, SmartBpRecognitionRegion Region, string TargetField, string ImageDataUrl);

/// <summary>One requested incremental snapshot recognition package.</summary>
public sealed record SmartBpSnapshotDeltaRequest(IReadOnlyList<(SmartBpRecognitionRegion Region, string TargetField)> RequestedRegions,
    IReadOnlyList<string> Diagnostics)
{
    /// <summary>Gets requested business content fields.</summary>
    public IReadOnlyList<string> RequestedFields => RequestedRegions.Select(item => item.TargetField).Distinct(StringComparer.Ordinal).ToArray();
}

/// <summary>Incremental model output containing phase and only requested field updates.</summary>
public sealed class SmartBpSnapshotDeltaResult
{
    /// <summary>Gets or sets the detected current phase.</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
    /// <summary>Gets or sets requested field updates.</summary>
    [JsonPropertyName("updates")] public List<SmartBpSnapshotFieldUpdate> Updates { get; set; } = [];
}

/// <summary>One field update in a snapshot delta result.</summary>
public sealed class SmartBpSnapshotFieldUpdate
{
    /// <summary>Gets or sets the business field id.</summary>
    [JsonPropertyName("field")] public string Field { get; set; } = "";
    /// <summary>Gets or sets slots for banned_sur, banned_hun or picked_sur.</summary>
    [JsonPropertyName("slots")] public List<SmartBpRecognizedPlayerCharacterSlot>? Slots { get; set; }
    /// <summary>Gets or sets the hunter pick slot when field is picked_hun.</summary>
    [JsonPropertyName("picked_hun")] public SmartBpRecognizedPlayerCharacterSlot? PickedHun { get; set; }
}

/// <summary>In-memory locally merged SmartBP recognition state.</summary>
public sealed class SmartBpRecognitionState
{
    /// <summary>Gets or sets the latest phase.</summary>
    public string Phase { get; set; } = "未知";
    /// <summary>Gets or sets known survivor bans.</summary>
    public List<SmartBpRecognizedCharacterSlot> BannedSur { get; set; } = DefaultBannedSur();
    /// <summary>Gets or sets known hunter bans.</summary>
    public List<SmartBpRecognizedCharacterSlot> BannedHun { get; set; } = DefaultBannedHun();
    /// <summary>Gets or sets known survivor picks or assignments.</summary>
    public List<SmartBpRecognizedPlayerCharacterSlot> PickedSur { get; set; } = DefaultPickedSur();
    /// <summary>Gets or sets known hunter pick.</summary>
    public SmartBpRecognizedPlayerCharacterSlot PickedHun { get; set; } = DefaultPickedHun();
    /// <summary>Gets or sets last update timestamp per field.</summary>
    public Dictionary<string, DateTimeOffset> FieldUpdatedAt { get; set; } = [];
    /// <summary>Gets or sets latest accepted frame sequence.</summary>
    public long LastFrameSequence { get; set; }
    /// <summary>Gets or sets latest accepted frame sequence per field.</summary>
    public Dictionary<string, long> FieldFrameSequences { get; set; } = [];

    /// <summary>Creates default survivor ban slots.</summary>
    public static List<SmartBpRecognizedCharacterSlot> DefaultBannedSur() => Enumerable.Range(0, 4).Select(i => new SmartBpRecognizedCharacterSlot { Index = i, CharacterName = "未选择" }).ToList();
    /// <summary>Creates default hunter ban slots.</summary>
    public static List<SmartBpRecognizedCharacterSlot> DefaultBannedHun() => Enumerable.Range(0, 2).Select(i => new SmartBpRecognizedCharacterSlot { Index = i, CharacterName = "未选择" }).ToList();
    /// <summary>Creates default survivor pick slots.</summary>
    public static List<SmartBpRecognizedPlayerCharacterSlot> DefaultPickedSur() => Enumerable.Range(0, 4).Select(i => new SmartBpRecognizedPlayerCharacterSlot { Index = i, CharacterName = "未选择" }).ToList();
    /// <summary>Creates the default hunter pick slot.</summary>
    public static SmartBpRecognizedPlayerCharacterSlot DefaultPickedHun() => new() { Index = 0, CharacterName = "未选择" };
}

/// <summary>Read-only recognition ledger snapshot.</summary>
public sealed record SmartBpRecognitionLedgerSnapshot(IReadOnlyCollection<SmartBpWorkflowOperationKey> CompletedKeys);

/// <summary>Legacy model-facing BP stage detection result.</summary>
public sealed class SmartBpStageDetectionResult
{
    /// <summary>Gets or sets schema version.</summary>
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    /// <summary>Gets or sets recognized action.</summary>
    [JsonPropertyName("recognized_action")] public string RecognizedAction { get; set; } = "Unknown";
    /// <summary>Gets or sets active side.</summary>
    [JsonPropertyName("active_side")] public string ActiveSide { get; set; } = "unknown";
    /// <summary>Gets or sets operation region.</summary>
    [JsonPropertyName("operation_region")] public string OperationRegion { get; set; } = "unknown";
    /// <summary>Gets or sets operation owner.</summary>
    [JsonPropertyName("operation_owner")] public string OperationOwner { get; set; } = "unknown";
    /// <summary>Gets or sets target camp.</summary>
    [JsonPropertyName("target_camp")] public string TargetCamp { get; set; } = "unknown";
    /// <summary>Gets or sets left-top title.</summary>
    [JsonPropertyName("left_top_title")] public string? LeftTopTitle { get; set; }
    /// <summary>Gets or sets right-top title.</summary>
    [JsonPropertyName("right_top_title")] public string? RightTopTitle { get; set; }
    /// <summary>Gets or sets main status.</summary>
    [JsonPropertyName("main_status")] public string? MainStatus { get; set; }
    /// <summary>Gets or sets confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
    /// <summary>Gets or sets evidence.</summary>
    [JsonPropertyName("evidence")] public List<string> Evidence { get; set; } = [];
    /// <summary>Gets or sets warnings.</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>Focused BP operation extraction result.</summary>
public sealed class SmartBpFocusedExtractionResult
{
    /// <summary>Gets or sets schema version.</summary>
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    /// <summary>Gets or sets task.</summary>
    [JsonPropertyName("task")] public string Task { get; set; } = "";
    /// <summary>Gets or sets operation region.</summary>
    [JsonPropertyName("operation_region")] public string OperationRegion { get; set; } = "unknown";
    /// <summary>Gets or sets target camp.</summary>
    [JsonPropertyName("target_camp")] public string TargetCamp { get; set; } = "unknown";
    /// <summary>Gets or sets extracted slots.</summary>
    [JsonPropertyName("slots")] public List<SmartBpVisionSlot> Slots { get; set; } = [];
    /// <summary>Gets or sets warnings.</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>Locally controlled detected operation kind.</summary>
public enum SmartBpDetectedOperationKind { BanCharacter, PickSurvivor, PickHunter, SwapSurvivors }

/// <summary>Controls workflow validation and animation behavior for one detected operation.</summary>
public enum SmartBpDetectedOperationApplyMode { CurrentStep, Backfill }

/// <summary>Preview candidate derived from focused visual extraction.</summary>
public sealed record SmartBpDetectedOperation(SmartBpDetectedOperationKind Kind, GameAction SourceGuidanceAction,
    IReadOnlyList<int> SourceGuidanceIndexes, Camp Camp, int SlotIndex, string? RawCharacterName,
    string? ResolvedCharacterKey, string? ResolvedCharacterName, string? PlayerId, double Confidence, string Reason,
    int? SourceWorkflowStepIndex = null,
    SmartBpDetectedOperationApplyMode ApplyMode = SmartBpDetectedOperationApplyMode.CurrentStep);

/// <summary>Stable ledger identity for one workflow-derived character operation.</summary>
public sealed record SmartBpWorkflowOperationKey(GameProgress GameProgress, int StepIndex, GameAction Action,
    int SlotIndex, Camp Camp, string? ResolvedCharacterKey);

/// <summary>Candidate operations associated with one immutable GameGuidance workflow step.</summary>
public sealed record SmartBpWorkflowStepCandidateSet(int StepIndex, GameAction Action, IReadOnlyList<int> Indexes,
    IReadOnlyList<SmartBpDetectedOperation> Operations, string Reason);

/// <summary>Ordered character backfill plan built from a merged region snapshot.</summary>
public sealed record SmartBpWorkflowBackfillPlan(IReadOnlyList<SmartBpWorkflowStepCandidateSet> StepCandidates,
    IReadOnlyList<string> Diagnostics);

/// <summary>Result of reconciling a detected stage with GameGuidance.</summary>
public sealed record SmartBpGuidanceSyncResult(bool Changed, bool IsAccepted, string Reason, GameAction? TargetAction,
    IReadOnlyList<int> TargetIndexes, int? TargetStepIndex);

/// <summary>Result of building preview candidate operations.</summary>
public sealed record SmartBpCandidateOperationBuildResult(
    IReadOnlyList<SmartBpDetectedOperation> Operations,
    IReadOnlyList<string> Messages);

/// <summary>Result of applying accepted candidate operations.</summary>
public sealed record SmartBpOperationApplyResult(int AppliedCount, int SkippedCount, IReadOnlyList<string> Messages);

/// <summary>One automatic recognition pipeline result.</summary>
public sealed record SmartBpAutoRecognitionTickResult(SmartBpBusinessStateRecognitionResult? BusinessState,
    SmartBpPhaseRecognitionResult? PhaseResult, SmartBpFocusedBusinessExtractionResult? FocusedResult,
    SmartBpCroppedFrame? PhaseCrop, SmartBpCroppedFrame? FocusedCrop,
    SmartBpGuidanceSyncResult? GuidanceSync, GameGuidanceRuntimeSnapshot GuidanceSnapshot,
    IReadOnlyList<SmartBpDetectedOperation> Operations, IReadOnlyList<string> CandidateMessages,
    SmartBpOperationApplyResult? ApplyResult, string RawJson, string? Error,
    SmartBpRegionSnapshot? RegionSnapshot = null,
    SmartBpWorkflowBackfillPlan? BackfillPlan = null,
    IReadOnlyList<SmartBpCroppedFrame>? ContentCrops = null);

/// <summary>Download state exposed to the UI.</summary>
public sealed record QwenDownloadState(bool IsDownloading, double? Progress, string Status);

/// <summary>A bundled recognition prompt profile.</summary>
public sealed record SmartBpPromptProfile(string Id, string DisplayName, string SystemPrompt);

/// <summary>llama.cpp runtime manifest root.</summary>
public sealed class LlamaCppRuntimeManifest
{
    /// <summary>Gets or sets schema version.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Gets or sets upstream runtime version.</summary>
    public string RuntimeVersion { get; set; } = "";
    /// <summary>Gets or sets release page.</summary>
    public string ReleasePage { get; set; } = "";
    /// <summary>Gets or sets runtime assets.</summary>
    public List<LlamaCppRuntimeAsset> Assets { get; set; } = [];
}

/// <summary>One installable llama.cpp runtime archive.</summary>
public sealed class LlamaCppRuntimeAsset
{
    /// <summary>Gets or sets asset id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets display name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gets or sets CPU architecture.</summary>
    public string Architecture { get; set; } = "";
    /// <summary>Gets or sets backend.</summary>
    public string Backend { get; set; } = "";
    /// <summary>Gets or sets archive URL.</summary>
    public string Url { get; set; } = "";
    /// <summary>Gets or sets optional SHA256.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Gets or sets executable filename.</summary>
    public string? EntryExe { get; set; }
    /// <summary>Gets or sets required extra asset ids.</summary>
    public List<string> RequiredExtraAssets { get; set; } = [];
}

/// <summary>Managed llama.cpp runtime installation state.</summary>
public sealed record LlamaCppRuntimeInstallState(bool IsDownloading, double? Progress, string Status);

/// <summary>Visual extraction result returned by the model.</summary>
public sealed class SmartBpVisionExtractionResult
{
    /// <summary>Gets or sets schema version.</summary>
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    /// <summary>Gets or sets scene information.</summary>
    [JsonPropertyName("scene")] public SmartBpVisionScene Scene { get; set; } = new();
    /// <summary>Gets or sets visible teams.</summary>
    [JsonPropertyName("teams")] public List<SmartBpVisionTeam> Teams { get; set; } = [];
    /// <summary>Gets or sets flattened visible characters.</summary>
    [JsonPropertyName("all_characters")] public List<SmartBpVisionCharacter> AllCharacters { get; set; } = [];
    /// <summary>Gets or sets flattened player IDs.</summary>
    [JsonPropertyName("all_player_ids")] public List<SmartBpVisionPlayerId> AllPlayerIds { get; set; } = [];
    /// <summary>Gets or sets recognition warnings.</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>Visual scene metadata.</summary>
public sealed class SmartBpVisionScene
{
    /// <summary>Gets or sets game name.</summary>
    [JsonPropertyName("game")] public string Game { get; set; } = "";
    /// <summary>Gets or sets interface type.</summary>
    [JsonPropertyName("interface_type")] public string InterfaceType { get; set; } = "";
    /// <summary>Gets or sets task.</summary>
    [JsonPropertyName("task")] public string Task { get; set; } = "";
    /// <summary>Gets or sets main status text.</summary>
    [JsonPropertyName("main_status")] public string? MainStatus { get; set; }
    /// <summary>Gets or sets pause status text.</summary>
    [JsonPropertyName("pause_status")] public string? PauseStatus { get; set; }
    /// <summary>Gets or sets pause remaining seconds.</summary>
    [JsonPropertyName("pause_remaining_seconds")] public double? PauseRemainingSeconds { get; set; }
}

/// <summary>One visual team region.</summary>
public sealed class SmartBpVisionTeam
{
    /// <summary>Gets or sets screen side.</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>Gets or sets faction.</summary>
    [JsonPropertyName("faction")] public string Faction { get; set; } = "unknown";
    /// <summary>Gets or sets title text.</summary>
    [JsonPropertyName("title_text")] public string? TitleText { get; set; }
    /// <summary>Gets or sets subtitle text.</summary>
    [JsonPropertyName("subtitle_text")] public string? SubtitleText { get; set; }
    /// <summary>Gets or sets slots.</summary>
    [JsonPropertyName("slots")] public List<SmartBpVisionSlot> Slots { get; set; } = [];
}

/// <summary>One visual slot.</summary>
public sealed class SmartBpVisionSlot
{
    /// <summary>Gets or sets slot index.</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>Gets or sets slot state.</summary>
    [JsonPropertyName("slot_state")] public string SlotState { get; set; } = "unknown";
    /// <summary>Gets or sets raw candidate character name.</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>Gets or sets player ID.</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>Gets or sets banned/unavailable flag.</summary>
    [JsonPropertyName("is_banned_or_unavailable")] public bool IsBannedOrUnavailable { get; set; }
    /// <summary>Gets or sets all visible raw text.</summary>
    [JsonPropertyName("raw_visible_text")] public string? RawVisibleText { get; set; }
    /// <summary>Gets or sets confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>One flattened visual character.</summary>
public sealed class SmartBpVisionCharacter
{
    /// <summary>Gets or sets character name.</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>Gets or sets faction.</summary>
    [JsonPropertyName("faction")] public string Faction { get; set; } = "unknown";
    /// <summary>Gets or sets player ID.</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>Gets or sets side.</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>Gets or sets slot index.</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>Gets or sets state.</summary>
    [JsonPropertyName("slot_state")] public string SlotState { get; set; } = "unknown";
    /// <summary>Gets or sets confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>One flattened visual player ID.</summary>
public sealed class SmartBpVisionPlayerId
{
    /// <summary>Gets or sets player ID.</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>Gets or sets character name.</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>Gets or sets side.</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>Gets or sets slot index.</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>Gets or sets confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>A normalized character occurrence.</summary>
public sealed record SmartBpNormalizedCharacter(string? RawCharacterName, string? ResolvedCharacterKey,
    string? ResolvedCharacterName, Camp Camp, int SlotIndex, double Confidence, IReadOnlyList<string> Warnings);

/// <summary>Recognition preview returned to the UI.</summary>
public sealed record SmartBpRecognitionPreview(string RawResponse, string ParsedVisualSummary,
    string ResolvedCharacterSummary, long ElapsedMilliseconds, int RecommendedIntervalMilliseconds, string? Error);

/// <summary>Built-in recognition sample.</summary>
public sealed record SmartBpTestFrame(string Id, string FileName, SmartBpRecognitionTask Task);

/// <summary>One timestamped AI pipeline diagnostic message.</summary>
public sealed class SmartBpDebugMessageEventArgs : EventArgs
{
    /// <summary>Initializes a diagnostic message.</summary>
    /// <param name="timestamp">Message timestamp.</param>
    /// <param name="source">Subsystem name.</param>
    /// <param name="message">Message text.</param>
    public SmartBpDebugMessageEventArgs(DateTimeOffset timestamp, string source, string message)
    {
        Timestamp = timestamp;
        Source = source;
        Message = message;
    }
    /// <summary>Gets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; }
    /// <summary>Gets the subsystem name.</summary>
    public string Source { get; }
    /// <summary>Gets the message text.</summary>
    public string Message { get; }
}
