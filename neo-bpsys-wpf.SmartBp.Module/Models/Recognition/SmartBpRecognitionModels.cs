using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Abstractions.Services;
using OpenCvSharp;

namespace neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

/// <summary>Supported AI recognition tasks.</summary>
public enum SmartBpRecognitionTask { DetectStage, BanSur, BanHun, PickSur, PickHun, CharacterDistribution, FullBpScan }

/// <summary>Supported SmartBP BP recognition engines.</summary>
public enum SmartBpRecognitionEngine { Ocr, AiQwen }

/// <summary>First-class SmartBP recognition strategy selected by the coordinator and UI.</summary>
public enum SmartBpRecognitionStrategy
{
    /// <summary>Use the selected local OCR provider only.</summary>
    PureOcr,
    /// <summary>Use the selected business vision model only.</summary>
    PureAi,
    /// <summary>Use business AI for scene and phase decisions, then local OCR for text extraction.</summary>
    AiWithOcr,
    /// <summary>Use business AI for scene and phase decisions, then a dedicated AI OCR model for text extraction.</summary>
    AiWithAiOcr
}

/// <summary>Chooses how hybrid recognition evidence is fused into SmartBP field updates.</summary>
public enum SmartBpHybridFusionMode
{
    /// <summary>Use local C# parsers/interpreters and merge in-process.</summary>
    LocalCSharp,
    /// <summary>Ask the Business AI model to convert evidence into structured BP field updates.</summary>
    BusinessAi
}

/// <summary>Expected JSON contract for Business AI transcript fusion output.</summary>
public enum SmartBpBusinessAiFusionOutputContract
{
    /// <summary>Return a complete BP business-state object with phase and all four BP fields.</summary>
    FullBusinessState,
    /// <summary>Return a snapshot delta object with phase and updates.</summary>
    SnapshotDelta
}

/// <summary>Family of a managed local vision model.</summary>
public enum LocalVisionModelFamily
{
    /// <summary>Qwen 3.5 vision-language models.</summary>
    Qwen35,
    /// <summary>GLM OCR models.</summary>
    GlmOcr,
    /// <summary>PaddleOCR-VL models.</summary>
    PaddleOcrVl,
    /// <summary>Custom or unknown local vision model family.</summary>
    Custom
}

/// <summary>Role a local vision model is expected to serve.</summary>
public enum LocalVisionModelRole
{
    /// <summary>Business VLM for scene, phase, and BP state recognition.</summary>
    BusinessVlm,
    /// <summary>AI OCR text extractor that should not own BP business interpretation.</summary>
    AiOcrTextExtractor,
    /// <summary>Model can be used for both business recognition and AI OCR extraction.</summary>
    Both,
    /// <summary>Role is unknown.</summary>
    Unknown
}

/// <summary>Identifies the source used to download a Qwen model profile.</summary>
public enum QwenModelSourceType { DirectUrl, HuggingFace }

/// <summary>Describes how a Qwen vision projector is supplied.</summary>
public enum QwenMmprojMode { Separate, Embedded, None }

/// <summary>Describes how a local vision projector is supplied.</summary>
public enum VisionProjectorMode { Separate, Embedded, None }

/// <summary>Role of a managed llama.cpp vision server.</summary>
public enum LlamaVisionServerRole
{
    /// <summary>Business AI server used for scene, phase, and BP business reasoning.</summary>
    BusinessAi,
    /// <summary>AI OCR server used only to extract visible text transcripts.</summary>
    AiOcr
}

/// <summary>Fine-grained Identity V scene used to gate BP recognition.</summary>
public enum SmartBpRecognitionScene
{
    Unknown, Lobby, RulesDialog, BanPickOrderDialog, Transition, CharacterBp,
    SurvivorTalent, HunterTalent, TalentLocked, AreaSelectionSurvivor,
    AreaSelectionHunter, WaitingGameStart, Loading, InGame, OutOfBp
}

/// <summary>Decision produced by the BP scene gate.</summary>
public sealed record SmartBpSceneGateResult(
    SmartBpRecognitionScene Scene,
    bool IsBpRecognitionAllowed,
    bool IsCharacterOperationAllowed,
    bool ShouldPauseAutomaticRecognition,
    string Reason);

/// <summary>Decision produced by the scene/phase controller.</summary>
public sealed class SmartBpScenePhaseDecision
{
    /// <summary>Gets the recognized scene.</summary>
    public SmartBpRecognitionScene Scene { get; init; }
    /// <summary>Gets the recognized phase text.</summary>
    public string Phase { get; init; } = "未知";
    /// <summary>Gets whether BP recognition is allowed in this scene.</summary>
    public bool BpRecognitionAllowed { get; init; }
    /// <summary>Gets whether character operations are allowed in this scene.</summary>
    public bool CharacterOperationAllowed { get; init; }
    /// <summary>Gets whether automatic recognition should pause.</summary>
    public bool ShouldPauseAutomaticRecognition { get; init; }
    /// <summary>Gets recommended business fields for the next extraction step.</summary>
    public IReadOnlyList<string> RecommendedFields { get; init; } = [];
    /// <summary>Gets a human-readable decision reason.</summary>
    public string Reason { get; init; } = "";
}

/// <summary>Controls how recognized operations are reconciled with the current game.</summary>
public enum SmartBpRecognitionApplyMode
{
    /// <summary>Reconciles recognition against the active GameGuidance workflow.</summary>
    GuidedWorkflow,
    /// <summary>Synchronizes recognized character slots without workflow context.</summary>
    FreeFullSync
}

/// <summary>Coarse SmartBP recognition crop regions.</summary>
public enum SmartBpRecognitionRegion
{
    /// <summary>BP phase title area.</summary>
    PhaseTop,
    /// <summary>Absolute top-left global game-status area.</summary>
    TopLeftStatus,
    /// <summary>Left-side upper BP content area.</summary>
    LeftTop,
    /// <summary>Right-side upper BP content area.</summary>
    RightTop,
    /// <summary>Left-side lower BP content area.</summary>
    LeftBottom,
    /// <summary>Right-side lower BP content area.</summary>
    RightBottom
}

/// <summary>Controls how many BP content regions are recognized for one snapshot.</summary>
public enum SmartBpRegionSnapshotRecognitionMode { FullAllRegions, PendingAndCurrentRegions }

/// <summary>Controls how the AI client requests structured JSON output from llama-server.</summary>
public enum AiStructuredOutputMode
{
    /// <summary>Sends <c>response_format=json_schema</c> and relies on the server to enforce the schema.</summary>
    JsonSchemaStrict,
    /// <summary>Omits <c>response_format</c>, asks for raw JSON in the prompt, and repairs Markdown fences locally.</summary>
    JsonPromptAndRepair
}

/// <summary>Identifies which recognition path the coordinator used for one tick.</summary>
public enum SmartBpRecognitionPath
{
    /// <summary>Only the phase crop was recognized because no business fields were requested.</summary>
    PhaseOnly,
    /// <summary>Independent per-field snapshot recognitions were run for the requested fields.</summary>
    FieldSnapshot,
    /// <summary>All four business fields were recognized as independent field snapshots.</summary>
    FullFieldSnapshot,
    /// <summary>Legacy multi-image snapshot delta recognition (model-side delta updates).</summary>
    LegacyDelta
}

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

/// <summary>Normalized result of detecting a status shown after character BP has ended.</summary>
public sealed class SmartBpPostBpStatusResult
{
    /// <summary>Gets whether the evidence identifies a post-BP scene.</summary>
    public bool IsPostBp { get; init; }
    /// <summary>Gets the normalized phase.</summary>
    public string Phase { get; init; } = "未知";
    /// <summary>Gets the normalized scene.</summary>
    public SmartBpRecognitionScene Scene { get; init; } = SmartBpRecognitionScene.Unknown;
    /// <summary>Gets the detection reason.</summary>
    public string Reason { get; init; } = "";
    /// <summary>Gets the source evidence.</summary>
    public string Evidence { get; init; } = "";
    /// <summary>Gets the fuzzy-match score.</summary>
    public double Score { get; init; }
}

/// <summary>Local vision model manifest root.</summary>
public sealed class LocalVisionModelManifest
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Gets or sets model profiles.</summary>
    public List<LocalVisionModelProfile> Models { get; set; } = [];
}

/// <summary>One Qwen model and its matching vision projector.</summary>
public sealed class QwenModelProfile
{
    /// <summary>Gets or sets the profile id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gets or sets the model family.</summary>
    public LocalVisionModelFamily Family { get; set; } = LocalVisionModelFamily.Custom;
    /// <summary>Gets or sets the intended model role.</summary>
    public LocalVisionModelRole Role { get; set; } = LocalVisionModelRole.Unknown;
    /// <summary>Gets or sets the model source type.</summary>
    public QwenModelSourceType SourceType { get; set; } = QwenModelSourceType.DirectUrl;
    /// <summary>Gets or sets the model URL for direct URL profiles.</summary>
    public string ModelUrl { get; set; } = "";
    /// <summary>Gets or sets the model filename.</summary>
    public string ModelFileName { get; set; } = "";
    /// <summary>Gets or sets the projector URL.</summary>
    public string? MmprojUrl { get; set; }
    /// <summary>Gets or sets the projector filename.</summary>
    public string? MmprojFileName { get; set; }
    /// <summary>Gets or sets the HuggingFace repository id.</summary>
    public string? HuggingFaceRepoId { get; set; }
    /// <summary>Gets or sets the HuggingFace revision.</summary>
    public string HuggingFaceRevision { get; set; } = "main";
    /// <summary>Gets or sets how the vision projector is supplied.</summary>
    public QwenMmprojMode MmprojMode { get; set; } = QwenMmprojMode.Separate;
    /// <summary>Gets or sets how the vision projector is supplied using the generic local vision terminology.</summary>
    public VisionProjectorMode ProjectorMode
    {
        get => MmprojMode switch
        {
            QwenMmprojMode.Separate => VisionProjectorMode.Separate,
            QwenMmprojMode.Embedded => VisionProjectorMode.Embedded,
            QwenMmprojMode.None => VisionProjectorMode.None,
            _ => VisionProjectorMode.Separate
        };
        set => MmprojMode = value switch
        {
            VisionProjectorMode.Separate => QwenMmprojMode.Separate,
            VisionProjectorMode.Embedded => QwenMmprojMode.Embedded,
            VisionProjectorMode.None => QwenMmprojMode.None,
            _ => QwenMmprojMode.Separate
        };
    }
    /// <summary>Gets or sets whether Chinese UI should prefer the HuggingFace mirror.</summary>
    public bool UseHuggingFaceMirrorForChineseUi { get; set; } = true;
    /// <summary>Gets or sets the optional model hash.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Gets or sets the optional projector hash.</summary>
    public string? MmprojSha256 { get; set; }
    /// <summary>Gets or sets whether this profile is the recommended default for its role.</summary>
    public bool Recommended { get; set; }
    /// <summary>Gets or sets whether this profile is experimental.</summary>
    public bool Experimental { get; set; }
    /// <summary>Gets or sets the default structured-output mode for this model.</summary>
    public AiStructuredOutputMode DefaultStructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonPromptAndRepair;
}

/// <summary>One local vision model and its matching vision projector.</summary>
public sealed class LocalVisionModelProfile
{
    /// <summary>Gets or sets the profile id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gets or sets the model family.</summary>
    public LocalVisionModelFamily Family { get; set; } = LocalVisionModelFamily.Custom;
    /// <summary>Gets or sets the intended model role.</summary>
    public LocalVisionModelRole Role { get; set; } = LocalVisionModelRole.Unknown;
    /// <summary>Gets or sets the model source type.</summary>
    public QwenModelSourceType SourceType { get; set; } = QwenModelSourceType.DirectUrl;
    /// <summary>Gets or sets the model URL for direct URL profiles.</summary>
    public string ModelUrl { get; set; } = "";
    /// <summary>Gets or sets the model filename.</summary>
    public string ModelFileName { get; set; } = "";
    /// <summary>Gets or sets the projector URL.</summary>
    public string? MmprojUrl { get; set; }
    /// <summary>Gets or sets the projector filename.</summary>
    public string? MmprojFileName { get; set; }
    /// <summary>Gets or sets the HuggingFace repository id.</summary>
    public string? HuggingFaceRepoId { get; set; }
    /// <summary>Gets or sets the HuggingFace revision.</summary>
    public string HuggingFaceRevision { get; set; } = "main";
    /// <summary>Gets or sets how the vision projector is supplied.</summary>
    public VisionProjectorMode ProjectorMode { get; set; } = VisionProjectorMode.Separate;
    /// <summary>Gets or sets the optional model hash.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Gets or sets the optional projector hash.</summary>
    public string? MmprojSha256 { get; set; }
    /// <summary>Gets or sets whether this profile is the recommended default for its role.</summary>
    public bool Recommended { get; set; }
    /// <summary>Gets or sets whether this profile is experimental.</summary>
    public bool Experimental { get; set; }
    /// <summary>Gets or sets the default structured-output mode for this model.</summary>
    public AiStructuredOutputMode DefaultStructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonPromptAndRepair;
}

/// <summary>Root document for managed RapidOCR model profiles.</summary>
public sealed class RapidOcrModelManifest
{
    /// <summary>Gets or sets the manifest schema version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Gets or sets the available model profiles.</summary>
    public List<RapidOcrModelProfile> Models { get; set; } = [];
}

/// <summary>One managed RapidOCR detector, classifier, recognizer, and dictionary set.</summary>
public sealed class RapidOcrModelProfile
{
    /// <summary>Gets or sets the stable profile id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets the user-facing profile name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gets or sets the user-facing profile description.</summary>
    public string Description { get; set; } = "";
    /// <summary>Gets or sets the upstream RapidOCR model manifest version.</summary>
    public string Version { get; set; } = "";
    /// <summary>Gets or sets the detector asset.</summary>
    public RapidOcrModelAsset Det { get; set; } = new();
    /// <summary>Gets or sets the angle-classifier asset.</summary>
    public RapidOcrModelAsset Cls { get; set; } = new();
    /// <summary>Gets or sets the recognizer asset.</summary>
    public RapidOcrModelAsset Rec { get; set; } = new();
    /// <summary>Gets or sets the recognition dictionary asset.</summary>
    public RapidOcrModelAsset Dict { get; set; } = new();
}

/// <summary>One downloadable RapidOCR model asset.</summary>
public sealed class RapidOcrModelAsset
{
    /// <summary>Gets or sets the installed filename.</summary>
    public string FileName { get; set; } = "";
    /// <summary>Gets or sets the repository-relative source path.</summary>
    public string RemotePath { get; set; } = "";
    /// <summary>Gets or sets the official direct download URL copied from RapidOCR's default model manifest.</summary>
    public string DownloadUrl { get; set; } = "";
    /// <summary>Gets or sets the expected SHA-256 of the downloaded source.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Gets or sets the deterministic source transform; supported values are Direct and PaddleCharacterDictionaryYaml.</summary>
    public string Transform { get; set; } = "Direct";
}

/// <summary>Resolved installed paths for one RapidOCR model profile.</summary>
/// <param name="ProfileId">Profile id.</param>
/// <param name="Directory">Profile directory.</param>
/// <param name="DetPath">Detector path.</param>
/// <param name="ClsPath">Classifier path.</param>
/// <param name="RecPath">Recognizer path.</param>
/// <param name="DictPath">Dictionary path.</param>
public sealed record RapidOcrInstalledPaths(
    string ProfileId,
    string Directory,
    string DetPath,
    string ClsPath,
    string RecPath,
    string DictPath);

/// <summary>Managed RapidOCR model readiness information.</summary>
/// <param name="ProfileId">Selected profile id.</param>
/// <param name="ModelDirectory">Selected profile directory.</param>
/// <param name="IsInstalled">Whether every required file is installed.</param>
/// <param name="MissingFiles">Missing installed filenames.</param>
/// <param name="IsUsingFallback">Whether bundled fallback assets are active.</param>
/// <param name="InstalledVersion">Installed upstream model version, if recorded.</param>
/// <param name="LatestVersion">Version declared by the bundled manifest.</param>
/// <param name="HasUpdate">Whether the installed version or profile fingerprint is stale.</param>
public sealed record RapidOcrModelStatus(
    string ProfileId,
    string ModelDirectory,
    bool IsInstalled,
    IReadOnlyList<string> MissingFiles,
    bool IsUsingFallback = false,
    string? InstalledVersion = null,
    string? LatestVersion = null,
    bool HasUpdate = false);

/// <summary>Result of comparing an installed RapidOCR profile with bundled and official manifests.</summary>
/// <param name="InstalledVersion">Installed version, if recorded.</param>
/// <param name="BundledVersion">Version available in the bundled SmartBP manifest.</param>
/// <param name="OfficialVersion">Version currently referenced by RapidOCR's official manifest.</param>
/// <param name="HasInstallableUpdate">Whether the bundled profile can update the installed files.</param>
/// <param name="IsBundledManifestCurrent">Whether SmartBP's bundled profile matches the official manifest version.</param>
public sealed record RapidOcrModelUpdateCheckResult(
    string? InstalledVersion,
    string BundledVersion,
    string OfficialVersion,
    bool HasInstallableUpdate,
    bool IsBundledManifestCurrent);

/// <summary>Persisted AI recognition settings.</summary>
public sealed class SmartBpRecognitionSettings
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Gets or sets llama-server path.</summary>
    public string LlamaServerExecutablePath { get; set; } = "";
    /// <summary>Gets or sets the loopback port.</summary>
    public int LlamaServerPort { get; set; } = 18080;
    /// <summary>Gets or sets the business AI server port.</summary>
    public int BusinessAiServerPort { get; set; } = 18080;
    /// <summary>Gets or sets the AI OCR server port.</summary>
    public int AiOcrServerPort { get; set; } = 18081;
    /// <summary>Gets or sets the timeout for one llama.cpp inference request.</summary>
    public int AiRequestTimeoutSeconds { get; set; } = 35;
    /// <summary>Gets or sets the timeout for llama.cpp startup.</summary>
    public int AiStartupTimeoutSeconds { get; set; } = 120;
    /// <summary>Gets or sets whether Chinese UI uses the HuggingFace mirror.</summary>
    public bool UseHuggingFaceMirrorForChineseUi { get; set; } = true;
    /// <summary>Gets or sets an optional HuggingFace endpoint override.</summary>
    public string HuggingFaceEndpointOverride { get; set; } = "";
    /// <summary>Gets or sets the llama.cpp context size.</summary>
    public int LlamaContextSize { get; set; } = 8192;
    /// <summary>Gets or sets selected Qwen profile.</summary>
    public string SelectedQwenModelId { get; set; } = "qwen3.5-2b-q4km";
    /// <summary>Gets or sets the selected business local vision model profile.</summary>
    public string SelectedBusinessAiModelId { get; set; } = "qwen3.5-2b-q4km";
    /// <summary>Gets or sets the selected AI OCR local vision model profile.</summary>
    public string SelectedAiOcrModelId { get; set; } = "paddleocr-vl-1.6-gguf";
    /// <summary>Gets or sets whether AI OCR should use its own llama.cpp server when models differ.</summary>
    public bool UseSeparateAiOcrServer { get; set; } = true;
    /// <summary>Gets or sets how AI + OCR fuses OCR evidence into business state.</summary>
    public SmartBpHybridFusionMode AiWithOcrFusionMode { get; set; } = SmartBpHybridFusionMode.LocalCSharp;
    /// <summary>Gets or sets how AI + AI OCR fuses transcript evidence into business state.</summary>
    public SmartBpHybridFusionMode AiWithAiOcrFusionMode { get; set; } = SmartBpHybridFusionMode.BusinessAi;
    /// <summary>Gets or sets how Business AI fusion requests structured output from llama.cpp.</summary>
    public AiStructuredOutputMode BusinessAiFusionStructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonPromptAndRepair;
    /// <summary>Gets or sets the Business AI fusion output contract used by AI + AI OCR full debug recognition.</summary>
    public SmartBpBusinessAiFusionOutputContract AiWithAiOcrFullDebugFusionContract { get; set; } = SmartBpBusinessAiFusionOutputContract.FullBusinessState;
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
    /// <summary>Gets or sets whether guidance synchronization follows backend page navigation.</summary>
    public bool EnableAutoGuidancePageNavigation { get; set; }
    /// <summary>Gets or sets whether accepted operations may be applied.</summary>
    public bool EnableAutoApplyRecognition { get; set; }
    /// <summary>Gets or sets the recognition application strategy.</summary>
    public SmartBpRecognitionApplyMode RecognitionApplyMode { get; set; } = SmartBpRecognitionApplyMode.GuidedWorkflow;
    /// <summary>Gets or sets whether AI completes the preceding step before moving guidance.</summary>
    public bool AiOneStepDelayedMode { get; set; } = true;
    /// <summary>Gets or sets consecutive unknown-phase frames required for hunter-talent inference.</summary>
    public int AiUnknownPhaseTalentInferenceFrames { get; set; } = 2;
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
    /// <summary>Gets or sets whether the legacy model-side snapshot delta recognition is used instead of field snapshots.</summary>
    public bool UseLegacySnapshotDeltaRecognition { get; set; }
    /// <summary>Gets or sets how the AI client requests structured JSON output from llama-server.</summary>
    public AiStructuredOutputMode StructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonSchemaStrict;
    /// <summary>Gets or sets the selected BP recognition engine.</summary>
    public SmartBpRecognitionEngine RecognitionEngine { get; set; } = SmartBpRecognitionEngine.Ocr;
    /// <summary>Gets or sets the selected SmartBP recognition strategy.</summary>
    public SmartBpRecognitionStrategy RecognitionStrategy { get; set; } = SmartBpRecognitionStrategy.PureOcr;
    /// <summary>Gets or sets whether OCR BP recognition is enabled.</summary>
    public bool EnableOcrBpRecognition { get; set; } = true;
    /// <summary>Gets or sets the OCR BP loop interval.</summary>
    public int OcrRecognitionIntervalMs { get; set; } = 300;
    /// <summary>Gets or sets the measured minimum OCR interval.</summary>
    public int MinimumOcrRecognitionIntervalMs { get; set; }
    /// <summary>Gets or sets the measured minimum AI interval.</summary>
    public int MinimumAiRecognitionIntervalMs { get; set; }
    /// <summary>Gets or sets when recognition speed was last measured.</summary>
    public DateTimeOffset? LastRecognitionSpeedTestAt { get; set; }
    /// <summary>Gets or sets the engine label used by the last speed test.</summary>
    public string LastRecognitionSpeedTestEngine { get; set; } = "";
    /// <summary>Gets or sets the performance-affecting configuration fingerprint.</summary>
    public string LastRecognitionSpeedTestConfigurationHash { get; set; } = "";
    /// <summary>Gets or sets how long an OCR-merged field may remain fresh.</summary>
    public int OcrFieldStaleMilliseconds { get; set; } = 1500;
    /// <summary>Gets or sets how many previous workflow steps OCR considers for backfill planning.</summary>
    public int OcrBackfillLookBehindSteps { get; set; } = 2;
    /// <summary>Gets or sets whether OCR should combine crops into one contact sheet.</summary>
    public bool UseOcrContactSheet { get; set; } = true;
    /// <summary>Gets or sets whether OCR debug overlay output is enabled.</summary>
    public bool EnableOcrDebugOverlay { get; set; }
    /// <summary>Gets or sets the explicitly selected OCR provider.</summary>
    public SmartBpOcrProviderMode OcrProviderMode { get; set; } = SmartBpOcrProviderMode.Paddle;
    /// <summary>Gets or sets the explicitly selected OCR provider for strategy-based recognition.</summary>
    public SmartBpOcrProviderMode SelectedOcrProviderMode { get; set; } = SmartBpOcrProviderMode.Paddle;
    /// <summary>Gets or sets the selected managed RapidOCR profile id.</summary>
    public string SelectedRapidOcrModelId { get; set; } = "ppocr-v5-zh-mobile";
    /// <summary>Gets or sets RapidOCR detector input padding.</summary>
    public int RapidOcrPadding { get; set; }
    /// <summary>Gets or sets the RapidOCR legacy maximum-side resize cap.</summary>
    public int RapidOcrMaxSideLen { get; set; } = 1024;
    /// <summary>Gets or sets the RapidOCR DB box score threshold.</summary>
    public double RapidOcrBoxScoreThreshold { get; set; } = 0.5;
    /// <summary>Gets or sets the RapidOCR DB bitmap threshold.</summary>
    public double RapidOcrBoxThreshold { get; set; } = 0.3;
    /// <summary>Gets or sets the RapidOCR DB polygon expansion ratio.</summary>
    public double RapidOcrUnclipRatio { get; set; } = 1.6;
    /// <summary>Gets or sets whether RapidOCR runs its angle classifier.</summary>
    public bool RapidOcrUseAngleClassifier { get; set; } = true;
    /// <summary>Gets or sets whether RapidOCR also tries a contrast-enhanced grayscale image.</summary>
    public bool RapidOcrUsePreprocessingVariants { get; set; }
    /// <summary>Gets or sets a legacy external Tesseract tessdata directory value. Managed downloads ignore this path.</summary>
    public string TesseractDataPath { get; set; } = "";
    /// <summary>Gets or sets the Tesseract language expression.</summary>
    public string TesseractLanguages { get; set; } = "chi_sim+eng";
    /// <summary>Gets or sets the default Tesseract page segmentation mode.</summary>
    public int TesseractDefaultPsm { get; set; } = 6;
    /// <summary>Gets or sets whether Tesseract may be used when selected.</summary>
    public bool EnableTesseractOcr { get; set; } = true;
    /// <summary>Gets or sets the maximum number of Tesseract preprocessing variants.</summary>
    public int TesseractMaxPreprocessVariants { get; set; } = 3;
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
    /// <summary>Gets or sets whether sequential per-region requests may be used when multi-image recognition fails.</summary>
    public bool AllowSequentialSnapshotFallback { get; set; } = true;
    /// <summary>Gets or sets whether automatic JSON schemas should use full candidate enums.</summary>
    public bool UseStrictCandidateEnumsInAutoSchema { get; set; }
    /// <summary>Gets or sets maximum encoded width for phase crops.</summary>
    public int PhaseCropMaxImageWidth { get; set; } = 640;
    /// <summary>Gets or sets maximum encoded width for content crops.</summary>
    public int ContentCropMaxImageWidth { get; set; } = 768;
    /// <summary>Gets or sets the phase-only response token budget.</summary>
    public int PhaseMaxTokens { get; set; } = 48;
    /// <summary>Gets or sets the incremental snapshot delta token budget.</summary>
    public int SnapshotDeltaMaxTokens { get; set; } = 768;
    /// <summary>Gets or sets the banned_sur field snapshot token budget.</summary>
    public int BannedSurFieldMaxTokens { get; set; } = 256;
    /// <summary>Gets or sets the banned_hun field snapshot token budget.</summary>
    public int BannedHunFieldMaxTokens { get; set; } = 192;
    /// <summary>Gets or sets the picked_sur field snapshot token budget.</summary>
    public int PickedSurFieldMaxTokens { get; set; } = 384;
    /// <summary>Gets or sets the picked_hun field snapshot token budget.</summary>
    public int PickedHunFieldMaxTokens { get; set; } = 192;
    /// <summary>Gets or sets the short commit hold before moving guidance to a newly detected phase.</summary>
    public int PhaseTransitionCommitHoldMilliseconds { get; set; } = 350;
    /// <summary>Gets or sets the maximum commit hold before allowing late backfill.</summary>
    public int PhaseTransitionCommitHoldMaxMilliseconds { get; set; } = 800;
    /// <summary>Gets or sets whether late no-animation backfill remains allowed after phase movement.</summary>
    public bool AllowLateBackfillAfterPhaseMoved { get; set; } = true;
    /// <summary>Gets or sets the rolling recognition frame buffer length.</summary>
    public int RecognitionFrameBufferMilliseconds { get; set; } = 1500;
    /// <summary>Gets or sets how far back transition finalization may inspect frames.</summary>
    public int RecognitionTransitionLookBehindMilliseconds { get; set; } = 800;
    /// <summary>Gets or sets the crop-change threshold.</summary>
    public double RecognitionCropChangeThreshold { get; set; } = 0.035;
    /// <summary>Gets or sets how many stable crop observations are preferred.</summary>
    public int RecognitionCropStableFrames { get; set; } = 2;
    /// <summary>Gets or sets whether llama.cpp runtime update checks are enabled.</summary>
    public bool EnableLlamaRuntimeUpdateCheck { get; set; } = true;
    /// <summary>Gets or sets the llama.cpp runtime update interval in hours.</summary>
    public int LlamaRuntimeUpdateCheckIntervalHours { get; set; } = 168;
    /// <summary>Gets or sets a custom remote llama.cpp runtime manifest API URL.</summary>
    public string LlamaRuntimeManifestApiUrl { get; set; } = "";
    /// <summary>Gets or sets the last llama.cpp runtime update check time.</summary>
    public DateTimeOffset? LastLlamaRuntimeUpdateCheckAt { get; set; }
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
    /// <summary>Gets or sets the local OCR match confidence; model JSON does not serialize this metadata.</summary>
    [JsonIgnore] public double RecognitionConfidence { get; set; } = 1;
    /// <summary>Gets or sets whether local OCR matching is safe for automatic application.</summary>
    [JsonIgnore] public bool IsAutoApplySafe { get; set; } = true;
    /// <summary>Gets or sets the OCR match diagnostic reason.</summary>
    [JsonIgnore] public string? RecognitionReason { get; set; }
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
    IReadOnlyList<string> Diagnostics,
    SmartBpBusinessStateRecognitionResult? CurrentKnownState = null)
{
    /// <summary>Gets requested business content fields.</summary>
    public IReadOnlyList<string> RequestedFields => RequestedRegions.Select(item => item.TargetField).Distinct(StringComparer.Ordinal).ToArray();
}

/// <summary>Model-visible state of one incremental snapshot slot.</summary>
public enum SmartBpRecognizedSlotState
{
    /// <summary>The crop clearly shows a selected character in this slot.</summary>
    Selected,
    /// <summary>The crop clearly shows an empty or unselected slot.</summary>
    Empty,
    /// <summary>The crop is not reliable enough, so local merge should preserve previous state.</summary>
    Unknown
}

/// <summary>One slot update in a snapshot delta response.</summary>
public sealed class SmartBpSnapshotDeltaSlot
{
    /// <summary>Gets or sets the visual slot index.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>Gets or sets selected, empty, or unknown slot evidence.</summary>
    [JsonPropertyName("slot_state")]
    public string SlotState { get; set; } = "unknown";

    /// <summary>Gets or sets the recognized candidate character name or 未选择.</summary>
    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = "未选择";

    /// <summary>Gets or sets the visible player id when the slot belongs to a picked character.</summary>
    [JsonPropertyName("player_id")]
    public string? PlayerId { get; set; }
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
    [JsonPropertyName("slots")] public List<SmartBpSnapshotDeltaSlot>? Slots { get; set; }
    /// <summary>Gets or sets the hunter pick slot when field is picked_hun.</summary>
    [JsonPropertyName("picked_hun")] public SmartBpSnapshotDeltaSlot? PickedHun { get; set; }
}

/// <summary>Phase-only AI recognition result produced by the phase-only recognition path.</summary>
public sealed class SmartBpAiPhaseOnlyResult
{
    /// <summary>Gets the recognized phase.</summary>
    public SmartBpPhaseRecognitionResult Phase { get; init; } = new();
    /// <summary>Gets the phase crop used by the model.</summary>
    public SmartBpCroppedFrame Crop { get; init; } = default!;
    /// <summary>Gets the absolute top-left global-status crop used by the model.</summary>
    public SmartBpCroppedFrame TopLeftStatusCrop { get; init; } = default!;
    /// <summary>Gets the raw model JSON response.</summary>
    public string RawJson { get; init; } = "";
    /// <summary>Gets recognition diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>One field-level AI snapshot recognition result.</summary>
public sealed class SmartBpAiFieldSnapshotResult
{
    /// <summary>Gets the business field id (banned_sur, banned_hun, picked_sur, picked_hun).</summary>
    public string Field { get; init; } = "";
    /// <summary>Gets the parsed field snapshot slots with slot_state evidence.</summary>
    public IReadOnlyList<SmartBpSnapshotDeltaSlot> Slots { get; init; } = [];
    /// <summary>Gets the hunter pick slot when the field is picked_hun.</summary>
    public SmartBpSnapshotDeltaSlot? PickedHun { get; init; }
    /// <summary>Gets the focused business extraction derived from the visible snapshot.</summary>
    public SmartBpFocusedBusinessExtractionResult FocusedResult { get; init; } = new();
    /// <summary>Gets the content crop used by the model.</summary>
    public SmartBpCroppedFrame Crop { get; init; } = default!;
    /// <summary>Gets the raw model JSON response.</summary>
    public string RawJson { get; init; } = "";
    /// <summary>Gets recognition diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
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
public enum SmartBpDetectedOperationApplyMode
{
    /// <summary>Applies an operation associated with the current guidance step.</summary>
    CurrentStep,
    /// <summary>Applies a late operation associated with an earlier workflow step.</summary>
    Backfill,
    /// <summary>Applies a no-animation operation without workflow validation.</summary>
    FreeSync
}

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
    IReadOnlyList<SmartBpCroppedFrame>? ContentCrops = null,
    SmartBpSceneGateResult? SceneGate = null);

/// <summary>Performance information returned by one llama.cpp response.</summary>
public sealed record LlamaCppResponseMetrics(
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    double? TokensPerSecond,
    long ElapsedMilliseconds);

/// <summary>Validated installed paths for a Qwen model profile.</summary>
public sealed record QwenInstalledPaths(string ModelPath, string? MmprojPath, QwenMmprojMode MmprojMode);

/// <summary>Result returned by the step commit scheduler.</summary>
public sealed record SmartBpStepCommitResult(SmartBpBusinessStateRecognitionResult Snapshot,
    SmartBpWorkflowBackfillPlan Plan,
    SmartBpOperationApplyResult? ApplyResult,
    SmartBpGuidanceSyncResult? GuidanceSync,
    IReadOnlyList<string> Diagnostics);

/// <summary>One frame kept in the rolling SmartBP recognition frame buffer.</summary>
public sealed record SmartBpBufferedFrame(long Sequence, BitmapSource Frame, DateTimeOffset Timestamp);

/// <summary>Lightweight crop-change analysis result.</summary>
public sealed record SmartBpCropChangeResult(SmartBpRecognitionRegion Region, long Sequence, double Difference, bool IsChanged, bool IsStable);

/// <summary>OCR text grouped by one SmartBP coarse recognition region.</summary>
public sealed class SmartBpOcrRegionText
{
    /// <summary>Gets the source coarse region.</summary>
    public SmartBpRecognitionRegion Region { get; init; }
    /// <summary>Gets OCR text lines using region-local coordinates.</summary>
    public IReadOnlyList<OcrTextLine> Lines { get; init; } = [];
}

/// <summary>OCR-based SmartBP BP recognition result.</summary>
public sealed class SmartBpOcrRecognitionResult
{
    /// <summary>Gets the locally classified BP phase.</summary>
    public SmartBpPhaseRecognitionResult Phase { get; init; } = new();
    /// <summary>Gets the locally parsed business state for the requested OCR regions.</summary>
    public SmartBpBusinessStateRecognitionResult BusinessState { get; init; } = new();
    /// <summary>Gets OCR text grouped by coarse region.</summary>
    public IReadOnlyList<SmartBpOcrRegionText> Regions { get; init; } = [];
    /// <summary>Gets bounded recognition diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>One technical text line extracted from an AI OCR model response.</summary>
public sealed class SmartBpAiOcrTranscriptLine
{
    /// <summary>Gets or sets visible text extracted for transport and debugging.</summary>
    public string Text { get; set; } = "";
}

/// <summary>AI OCR transcript recognition result.</summary>
public sealed class SmartBpAiOcrTranscriptResult
{
    /// <summary>Gets technical transcript lines extracted without business interpretation.</summary>
    public IReadOnlyList<SmartBpAiOcrTranscriptLine> Lines { get; init; } = [];
    /// <summary>Gets raw output returned by the AI OCR model.</summary>
    public string RawJson { get; init; } = "";
    /// <summary>Gets bounded diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>AI OCR transcript evidence for one coarse BP business region.</summary>
public sealed class SmartBpAiOcrTranscriptRegionEvidence
{
    /// <summary>Gets the source SmartBP coarse region.</summary>
    public SmartBpRecognitionRegion Region { get; init; }
    /// <summary>Gets the SmartBP business field represented by the region.</summary>
    public string Field { get; init; } = "";
    /// <summary>Gets the AI OCR model id that produced this evidence.</summary>
    public string AiOcrModel { get; init; } = "";
    /// <summary>Gets the raw output returned by the AI OCR model.</summary>
    public string RawOutput { get; init; } = "";
    /// <summary>Gets technical transcript lines extracted without semantic cleanup.</summary>
    public IReadOnlyList<string> TechnicalLines { get; init; } = [];
}

/// <summary>Detailed local parse result for one OCR coarse region.</summary>
public sealed class SmartBpOcrParsedRegionResult
{
    /// <summary>Gets the parsed business result.</summary>
    public SmartBpFocusedBusinessExtractionResult Result { get; init; } = new();
    /// <summary>Gets resolver and parser diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    /// <summary>Gets whether a required role slot remains unresolved.</summary>
    public bool HasCriticalUnresolvedField { get; init; }
    /// <summary>Gets whether every resolved slot is safe for automatic application.</summary>
    public bool IsAutoApplySafe { get; init; }
}

/// <summary>OCR BP recognition request.</summary>
/// <param name="ContentRegions">Content regions to parse in this tick.</param>
/// <param name="IncludePhase">Whether to include the phase region.</param>
public sealed record SmartBpOcrRecognitionRequest(
    IReadOnlyList<SmartBpRecognitionRegion> ContentRegions,
    bool IncludePhase = true);

/// <summary>One contact-sheet image containing multiple OCR crops.</summary>
/// <param name="Image">Stacked OCR image.</param>
/// <param name="Regions">Coordinate mappings from sheet space to SmartBP regions.</param>
public sealed record SmartBpOcrContactSheet(
    Mat Image,
    IReadOnlyList<SmartBpOcrContactSheetRegion> Regions) : IDisposable
{
    /// <summary>Disposes the backing OpenCV image.</summary>
    public void Dispose() => Image.Dispose();
}

/// <summary>Mapping for one OCR contact-sheet crop.</summary>
/// <param name="Region">Source SmartBP region.</param>
/// <param name="SheetRect">Region rectangle in contact-sheet coordinates.</param>
/// <param name="OriginalFrameRect">Region rectangle in original frame coordinates.</param>
public sealed record SmartBpOcrContactSheetRegion(
    SmartBpRecognitionRegion Region,
    Rect SheetRect,
    Rect OriginalFrameRect);

/// <summary>Download state exposed to the UI.</summary>
/// <param name="IsDownloading">Whether a download is currently running.</param>
/// <param name="Progress">Overall progress percentage, when known.</param>
/// <param name="Status">Localization key or status text.</param>
/// <param name="CurrentFileName">Current file name.</param>
/// <param name="BytesReceived">Downloaded byte count.</param>
/// <param name="TotalBytes">Expected byte count.</param>
/// <param name="BytesPerSecond">Estimated download speed.</param>
/// <param name="Eta">Estimated remaining time.</param>
/// <param name="ErrorMessage">Detailed error message when the operation failed.</param>
public record SmartBpDownloadState(bool IsDownloading, double? Progress, string Status,
    string? CurrentFileName = null,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? Eta = null,
    string? ErrorMessage = null);

/// <summary>Describes one downloadable Tesseract language data asset.</summary>
/// <param name="Language">Tesseract language identifier.</param>
/// <param name="DisplayNameKey">Localization key for display.</param>
public sealed record TesseractLanguageAsset(string Language, string DisplayNameKey);

/// <summary>Describes required Tesseract language data in one tessdata directory.</summary>
/// <param name="IsInstalled">Whether every required language is installed.</param>
/// <param name="DataPath">Effective tessdata directory.</param>
/// <param name="MissingLanguages">Missing required language identifiers.</param>
/// <param name="InstalledLanguages">Installed required language identifiers.</param>
public sealed record TesseractDataStatus(bool IsInstalled, string DataPath,
    IReadOnlyList<string> MissingLanguages, IReadOnlyList<string> InstalledLanguages);

/// <summary>One optional AI runtime performance sample.</summary>
/// <param name="GpuName">GPU display name.</param>
/// <param name="GpuUtilizationPercent">GPU utilization percentage.</param>
/// <param name="VramUsedBytes">Used video memory.</param>
/// <param name="VramTotalBytes">Total video memory.</param>
/// <param name="ProcessId">Managed llama-server process identifier.</param>
/// <param name="UpdatedAt">Sample timestamp.</param>
/// <param name="IsAvailable">Whether NVML telemetry was available.</param>
public sealed record SmartBpAiPerformanceSnapshot(string GpuName, uint? GpuUtilizationPercent,
    ulong? VramUsedBytes, ulong? VramTotalBytes, int? ProcessId, DateTimeOffset UpdatedAt, bool IsAvailable);

/// <summary>Qwen download state exposed to the UI.</summary>
public sealed record QwenDownloadState(bool IsDownloading, double? Progress, string Status,
    string? CurrentFileName = null,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? Eta = null,
    string? ErrorMessage = null) : SmartBpDownloadState(IsDownloading, Progress, Status, CurrentFileName, BytesReceived, TotalBytes, BytesPerSecond, Eta, ErrorMessage);

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
    /// <summary>Gets or sets optional check interval from the manifest.</summary>
    public int? CheckIntervalHours { get; set; }
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
    /// <summary>Gets or sets whether the URL already points to a final downloadable file.</summary>
    public bool UrlIsDirectDownload { get; set; }
}

/// <summary>Managed llama.cpp runtime installation state.</summary>
public sealed record LlamaCppRuntimeInstallState(bool IsDownloading, double? Progress, string Status,
    string? CurrentFileName = null,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? Eta = null,
    string? ErrorMessage = null) : SmartBpDownloadState(IsDownloading, Progress, Status, CurrentFileName, BytesReceived, TotalBytes, BytesPerSecond, Eta, ErrorMessage);

/// <summary>Result of checking for llama.cpp runtime updates.</summary>
public sealed record LlamaCppRuntimeUpdateCheckResult(bool Checked, bool HasUpdate, string CurrentVersion,
    string? LatestVersion, IReadOnlyList<LlamaCppRuntimeAsset> LatestAssets, string Message);

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
    string? ResolvedCharacterName, Camp Camp, int SlotIndex, double Confidence, IReadOnlyList<string> Warnings,
    string MatchMode = "none", bool IsAutoApplySafe = false, string? RecognitionReason = null);

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
