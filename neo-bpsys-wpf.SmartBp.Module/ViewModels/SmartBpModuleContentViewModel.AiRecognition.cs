using System.Text;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using neo_bpsys_wpf.SmartBp.Module.Services.Recognition;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SmartBpModuleContentViewModel
{
    private readonly DispatcherTimer _aiPreviewTimer = new();
    private readonly DispatcherTimer _aiPerformanceTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private int _recognitionBusy;
    private bool _isSwitchingQwenModel;
    private bool _isSwitchingAiOcrModel;
    private bool _isAutomaticRecognitionStopPendingAfterQueueDrain;
    private int _automaticRecognitionUnavailableFrameCount;
    private LocalVisionModelDownloadRole? _activeVisionModelDownloadRole;

    private enum LocalVisionModelDownloadRole
    {
        BusinessAi,
        AiOcr
    }
    /// <summary>获取可用的识别应用模式。</summary>
    public IReadOnlyList<SmartBpRecognitionApplyMode> RecognitionApplyModes { get; } = Enum.GetValues<SmartBpRecognitionApplyMode>();
    /// <summary>获取可用的混合融合模式。</summary>
    public IReadOnlyList<SmartBpHybridFusionMode> HybridFusionModes { get; } = Enum.GetValues<SmartBpHybridFusionMode>();

    /// <summary>获取可用的内置测试帧。</summary>
    public IReadOnlyList<SmartBpTestFrame> AiTestFrames { get; } =
    [
        new("ban-sur-16x9", "ban-sur-16x9.png", SmartBpRecognitionTask.BanSur),
        new("ban-hun-16x9", "ban-hun-16x9.png", SmartBpRecognitionTask.BanHun),
        new("pick-sur-16x9", "pick-sur-16x9.png", SmartBpRecognitionTask.PickSur),
        new("pick-hun-16x9", "pick-hun-16x9.png", SmartBpRecognitionTask.PickHun),
        new("character-distribution-16x9", "character-distribution-16x9.png", SmartBpRecognitionTask.CharacterDistribution)
    ];
    [ObservableProperty]
    public partial SmartBpTestFrame? SelectedAiTestFrame { get; set; }

    [ObservableProperty]
    public partial string QwenManifestStatus { get; set; } = "SmartBpAiStatusLoading";

    [ObservableProperty]
    public partial string QwenModelProfile { get; set; } = "-";

    [ObservableProperty]
    public partial string QwenMmprojProfile { get; set; } = "-";

    [ObservableProperty]
    public partial IReadOnlyList<QwenModelProfile> QwenModelProfiles { get; set; } = [];

    [NotifyCanExecuteChangedFor(nameof(DownloadQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedQwenModelCommand))]
    [ObservableProperty]
    public partial QwenModelProfile? SelectedQwenModelProfile { get; set; }

    [ObservableProperty]
    public partial string CurrentQwenModelDisplayName { get; set; } = "";

    [NotifyCanExecuteChangedFor(nameof(DownloadQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedQwenModelCommand))]
    [ObservableProperty]
    public partial bool IsBusinessAiModelDownloading { get; set; }

    [ObservableProperty]
    public partial double BusinessAiModelDownloadProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBusinessAiModelDownloadDetail))]
    public partial string BusinessAiModelDownloadDetail { get; set; } = "";

    [ObservableProperty]
    public partial bool IsQwenInstalled { get; set; }

    [NotifyCanExecuteChangedFor(nameof(DownloadQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedQwenModelCommand))]
    [ObservableProperty]
    public partial bool IsQwenDownloading { get; set; }

    [NotifyCanExecuteChangedFor(nameof(DownloadQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteQwenModelCommand))]
    [ObservableProperty]
    public partial bool IsSelectedQwenModelInstalled { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<QwenModelProfile> AiOcrModelProfiles { get; set; } = [];

    [NotifyCanExecuteChangedFor(nameof(DownloadAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedAiOcrModelCommand))]
    [ObservableProperty]
    public partial QwenModelProfile? SelectedAiOcrModelProfile { get; set; }

    [ObservableProperty]
    public partial string CurrentAiOcrModelDisplayName { get; set; } = "";

    [ObservableProperty]
    public partial string AiOcrModelStatus { get; set; } = "-";

    [NotifyCanExecuteChangedFor(nameof(DownloadAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedAiOcrModelCommand))]
    [ObservableProperty]
    public partial bool IsAiOcrModelDownloading { get; set; }

    [ObservableProperty]
    public partial double AiOcrModelDownloadProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiOcrModelDownloadDetail))]
    public partial string AiOcrModelDownloadDetail { get; set; } = "";

    [NotifyCanExecuteChangedFor(nameof(DownloadAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedAiOcrModelCommand))]
    [ObservableProperty]
    public partial bool IsSelectedAiOcrModelInstalled { get; set; }

    [ObservableProperty]
    public partial double QwenDownloadProgress { get; set; }

    [ObservableProperty]
    public partial string QwenDownloadStatus { get; set; } = "-";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQwenDownloadDetail))]
    public partial string QwenDownloadDetail { get; set; } = "";

    [ObservableProperty]
    public partial string LlamaServerExecutablePath { get; set; } = "";

    [ObservableProperty]
    public partial string LlamaServerStatus { get; set; } = "SmartBpAiStatusStopped";

    [NotifyCanExecuteChangedFor(nameof(StartAiPreviewLoopCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopAiPreviewLoopCommand))]
    [ObservableProperty]
    public partial bool IsAiRecognizing { get; set; }

    [ObservableProperty]
    public partial bool IsAiPreviewLoopRunning { get; set; }

    [ObservableProperty]
    public partial string AiRawResponse { get; set; } = "";

    [ObservableProperty]
    public partial string AiNormalizedResult { get; set; } = "";

    [ObservableProperty]
    public partial long AiElapsedMilliseconds { get; set; }

    [ObservableProperty]
    public partial int AiRecommendedIntervalMilliseconds { get; set; }

    [ObservableProperty]
    public partial string AiLastError { get; set; } = "";

    [ObservableProperty]
    public partial string AiDebugLogText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsDebugLogEnabled { get; set; } = true;

    [ObservableProperty]
    public partial IReadOnlyList<SmartBpPromptProfile> AiPromptProfiles { get; set; } = [];

    [ObservableProperty]
    public partial SmartBpPromptProfile? SelectedAiPromptProfile { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<LlamaCppRuntimeAssetSelection> LlamaRuntimeAssets { get; set; } = [];

    [ObservableProperty]
    public partial LlamaCppRuntimeAssetSelection? SelectedLlamaRuntimeAsset { get; set; }

    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RollbackLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceStopLlamaServerCommand))]
    [ObservableProperty]
    public partial bool IsLlamaRuntimeInstalled { get; set; }

    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [ObservableProperty]
    public partial bool IsLlamaRuntimeDownloading { get; set; }

    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RollbackLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceStopLlamaServerCommand))]
    [ObservableProperty]
    public partial bool IsLlamaServerRunning { get; set; }

    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RollbackLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceStopLlamaServerCommand))]
    [ObservableProperty]
    public partial bool IsLlamaServerStarting { get; set; }

    [ObservableProperty]
    public partial double LlamaRuntimeDownloadProgress { get; set; }

    [ObservableProperty]
    public partial string LlamaRuntimeDownloadStatus { get; set; } = "-";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLlamaRuntimeDownloadDetail))]
    public partial string LlamaRuntimeDownloadDetail { get; set; } = "";

    [ObservableProperty]
    public partial string ManagedLlamaServerExecutablePath { get; set; } = "-";

    [ObservableProperty]
    public partial string LlamaRuntimeUpdateStatus { get; set; } = "-";

    [ObservableProperty]
    public partial bool EnableAutoGuidanceSync { get; set; }

    [ObservableProperty]
    public partial bool EnableAutoApplyRecognition { get; set; }

    [ObservableProperty]
    public partial bool EnableAutoGuidancePageNavigation { get; set; }

    [ObservableProperty]
    public partial bool EnableSmartBpProgressAutoCorrection { get; set; }

    [ObservableProperty]
    public partial string LastSmartBpProgressDiagnosis { get; set; } = "-";

    [ObservableProperty]
    public partial SmartBpRecognitionApplyMode RecognitionApplyMode { get; set; }

    [ObservableProperty]
    public partial bool AiOneStepDelayedMode { get; set; } = true;

    [ObservableProperty]
    public partial int AiUnknownPhaseTalentInferenceFrames { get; set; } = 2;

    [ObservableProperty]
    public partial bool PlayBackfillAnimations { get; set; }

    [ObservableProperty]
    public partial bool UseMultiImageSnapshotRequest { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<RecognitionStrategySelection> RecognitionStrategies { get; set; } = [];

    [ObservableProperty]
    public partial RecognitionStrategySelection? SelectedRecognitionStrategy { get; set; }

    [ObservableProperty]
    public partial bool IsOcrRecognitionEngine { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAiQwenRecognitionEngine { get; set; }

    [ObservableProperty]
    public partial bool IsPaddleRecognitionEngine { get; set; } = true;

    [ObservableProperty]
    public partial bool IsTesseractRecognitionEngine { get; set; }

    [ObservableProperty]
    public partial bool IsRapidRecognitionEngine { get; set; }

    [ObservableProperty]
    public partial bool IsBusinessAiModelVisible { get; set; }

    [ObservableProperty]
    public partial bool IsOcrProviderCardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAiOcrModelVisible { get; set; }

    [ObservableProperty]
    public partial bool IsAiWithOcrFusionModeVisible { get; set; }

    [ObservableProperty]
    public partial bool IsAiWithAiOcrFusionModeVisible { get; set; }

    [ObservableProperty]
    public partial SmartBpHybridFusionMode AiWithOcrFusionMode { get; set; } = SmartBpHybridFusionMode.LocalCSharp;

    [ObservableProperty]
    public partial SmartBpHybridFusionMode AiWithAiOcrFusionMode { get; set; } = SmartBpHybridFusionMode.BusinessAi;

    [ObservableProperty]
    public partial bool EnableOcrBpRecognition { get; set; } = true;

    [ObservableProperty]
    public partial int RecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial int OcrRecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial int OcrFieldStaleMilliseconds { get; set; }

    [ObservableProperty]
    public partial int OcrBackfillLookBehindSteps { get; set; }

    [ObservableProperty]
    public partial bool UseOcrContactSheet { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableOcrDebugOverlay { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<OcrProviderSelection> OcrProviders { get; set; } = [];

    [ObservableProperty]
    public partial OcrProviderSelection? SelectedOcrProvider { get; set; }

    [ObservableProperty]
    public partial string PaddleOcrStatus { get; set; } = "-";

    [ObservableProperty]
    public partial string TesseractOcrStatus { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsTesseractDataDownloading { get; set; }

    [ObservableProperty]
    public partial double TesseractDownloadProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTesseractDownloadDetail))]
    public partial string TesseractDownloadDetail { get; set; } = "";

    [ObservableProperty]
    public partial string TesseractLanguages { get; set; } = "chi_sim+eng";

    [ObservableProperty]
    public partial IReadOnlyList<TesseractLanguageSelection> TesseractLanguageOptions { get; set; } = [];

    [ObservableProperty]
    public partial bool EnableTesseractOcr { get; set; } = true;

    [ObservableProperty]
    public partial int TesseractDefaultPsm { get; set; } = 6;

    [ObservableProperty]
    public partial int TesseractMaxPreprocessVariants { get; set; } = 3;

    [ObservableProperty]
    public partial IReadOnlyList<RapidOcrModelProfile> RapidOcrModelProfiles { get; set; } = [];

    [ObservableProperty]
    public partial RapidOcrModelProfile? SelectedRapidOcrModelProfile { get; set; }

    [ObservableProperty]
    public partial string RapidOcrStatus { get; set; } = "-";

    [ObservableProperty]
    public partial string RapidOcrModelDirectory { get; set; } = "-";

    [ObservableProperty]
    public partial string RapidOcrInstalledVersion { get; set; } = "-";

    [ObservableProperty]
    public partial string RapidOcrLatestVersion { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsRapidOcrUpdateAvailable { get; set; }

    [NotifyCanExecuteChangedFor(nameof(DownloadRapidOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRapidOcrModelCommand))]
    [ObservableProperty]
    public partial bool IsSelectedRapidOcrModelInstalled { get; set; }

    [ObservableProperty]
    public partial string RapidOcrInstallActionText { get; set; } = "-";

    [NotifyCanExecuteChangedFor(nameof(DownloadRapidOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRapidOcrModelCommand))]
    [ObservableProperty]
    public partial bool IsRapidOcrDownloading { get; set; }

    [ObservableProperty]
    public partial double RapidOcrDownloadProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRapidOcrDownloadDetail))]
    public partial string RapidOcrDownloadDetail { get; set; } = "";

    [ObservableProperty]
    public partial int RapidOcrPadding { get; set; }

    [ObservableProperty]
    public partial int RapidOcrMaxSideLen { get; set; } = 1024;

    [ObservableProperty]
    public partial double RapidOcrBoxScoreThreshold { get; set; } = .5;

    [ObservableProperty]
    public partial double RapidOcrBoxThreshold { get; set; } = .3;

    [ObservableProperty]
    public partial double RapidOcrUnclipRatio { get; set; } = 1.6;

    [ObservableProperty]
    public partial bool RapidOcrUseAngleClassifier { get; set; } = true;

    [ObservableProperty]
    public partial bool RapidOcrUsePreprocessingVariants { get; set; }

    [ObservableProperty]
    public partial bool AllowSequentialSnapshotFallback { get; set; }

    [ObservableProperty]
    public partial bool UseStrictCandidateEnumsInAutoSchema { get; set; }

    [ObservableProperty]
    public partial int PhaseCropMaxImageWidth { get; set; }

    [ObservableProperty]
    public partial int ContentCropMaxImageWidth { get; set; }

    [ObservableProperty]
    public partial int PhaseMaxTokens { get; set; }

    [ObservableProperty]
    public partial int SnapshotDeltaMaxTokens { get; set; }

    [ObservableProperty]
    public partial int PhaseTransitionCommitHoldMilliseconds { get; set; }

    [ObservableProperty]
    public partial int PhaseTransitionCommitHoldMaxMilliseconds { get; set; }

    [ObservableProperty]
    public partial int RecognitionBackfillLookBehindSteps { get; set; }

    [ObservableProperty]
    public partial int RecognitionFieldStaleMilliseconds { get; set; }

    [ObservableProperty]
    public partial int RecognitionVisualBufferMilliseconds { get; set; }

    [ObservableProperty]
    public partial int LlamaParallelSlots { get; set; }

    [ObservableProperty]
    public partial int LlamaGpuLayers { get; set; }

    [ObservableProperty]
    public partial bool LlamaFlashAttention { get; set; }

    [ObservableProperty]
    public partial int LlamaBatchSize { get; set; }

    [ObservableProperty]
    public partial int LlamaUBatchSize { get; set; }

    [ObservableProperty]
    public partial string AiStageDetectionResult { get; set; } = "-";

    [ObservableProperty]
    public partial string AiGuidanceSnapshot { get; set; } = "-";

    [ObservableProperty]
    public partial string AiCandidateOperations { get; set; } = "-";

    [ObservableProperty]
    public partial BitmapSource? AiPhaseCropPreview { get; set; }

    [ObservableProperty]
    public partial BitmapSource? AiFocusedCropPreview { get; set; }

    [ObservableProperty]
    public partial string AiCropDebugInfo { get; set; } = "-";

    [ObservableProperty]
    public partial string RecognitionSpeedTestStatus { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentRecognitionEngineText { get; set; } = "-";

    [ObservableProperty]
    public partial int CurrentRecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial int MinimumRecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial string RecognitionIntervalEditHint { get; set; } = "-";

    [ObservableProperty]
    public partial string AiSceneDiagnostics { get; set; } = "-";

    [ObservableProperty]
    public partial string AiRequestMetrics { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsRecognitionSpeedTesting { get; set; }

    [ObservableProperty]
    public partial bool IsRecognitionIntervalEditable { get; set; }

    [ObservableProperty]
    public partial string AiGpuName { get; set; } = "not available";

    [ObservableProperty]
    public partial string AiGpuUtilization { get; set; } = "not available";

    [ObservableProperty]
    public partial string AiVramUsage { get; set; } = "not available";

    [ObservableProperty]
    public partial string AiLlamaProcessId { get; set; } = "-";

    [ObservableProperty]
    public partial string AiPerformanceUpdatedAt { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugStrategySummary { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugModeSummary { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugFinalBusinessState { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugFusionSummary { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugPhaseScene { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugBusinessAiRaw { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugPureAiFullRaw { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugOcrRawLines { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugAiOcrTranscript { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugParsedState { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugMergeLog { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugCandidateOperations { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugServerStatus { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugTiming { get; set; } = "-";

    [ObservableProperty]
    public partial string RecognitionDebugLogText { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsRecognitionDebugLogAutoScrollEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string BusinessAiServerStatus { get; set; } = "-";

    [ObservableProperty]
    public partial string BusinessAiServerProcessId { get; set; } = "-";

    [ObservableProperty]
    public partial string BusinessAiServerPortText { get; set; } = "-";

    [ObservableProperty]
    public partial string BusinessAiServerModelText { get; set; } = "-";

    [ObservableProperty]
    public partial string BusinessAiServerActivityText { get; set; } = "-";

    [NotifyCanExecuteChangedFor(nameof(StartBusinessAiServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRequiredLlamaServersCommand))]
    [ObservableProperty]
    public partial bool IsBusinessAiServerStarting { get; set; }

    [ObservableProperty]
    public partial string AiOcrServerStatus { get; set; } = "-";

    [ObservableProperty]
    public partial string AiOcrServerProcessId { get; set; } = "-";

    [ObservableProperty]
    public partial string AiOcrServerPortText { get; set; } = "-";

    [ObservableProperty]
    public partial string AiOcrServerModelText { get; set; } = "-";

    [ObservableProperty]
    public partial string AiOcrServerActivityText { get; set; } = "-";

    [ObservableProperty]
    public partial string AiOcrServerReuseStatus { get; set; } = "-";

    [NotifyCanExecuteChangedFor(nameof(StartAiOcrServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartRequiredLlamaServersCommand))]
    [ObservableProperty]
    public partial bool IsAiOcrServerStarting { get; set; }

    [NotifyCanExecuteChangedFor(nameof(StartRequiredLlamaServersCommand))]
    [ObservableProperty]
    public partial bool IsRequiredLlamaServersStarting { get; set; }

    private SmartBpRecognitionLayoutProfile? _aiRegionProfile;

    /// <summary>是否显示 Qwen 模型下载详情。</summary>
    public bool HasQwenDownloadDetail => !string.IsNullOrWhiteSpace(QwenDownloadDetail);
    /// <summary>是否显示业务 AI 模型下载详情。</summary>
    public bool HasBusinessAiModelDownloadDetail => !string.IsNullOrWhiteSpace(BusinessAiModelDownloadDetail);
    /// <summary>是否显示 AI OCR 模型下载详情。</summary>
    public bool HasAiOcrModelDownloadDetail => !string.IsNullOrWhiteSpace(AiOcrModelDownloadDetail);

    /// <summary>是否显示 llama.cpp 运行时下载详情。</summary>
    public bool HasLlamaRuntimeDownloadDetail => !string.IsNullOrWhiteSpace(LlamaRuntimeDownloadDetail);

    /// <summary>是否显示 Tesseract 语言数据下载详情。</summary>
    public bool HasTesseractDownloadDetail => !string.IsNullOrWhiteSpace(TesseractDownloadDetail);

    /// <summary>是否显示 RapidOCR 模型下载详情。</summary>
    public bool HasRapidOcrDownloadDetail => !string.IsNullOrWhiteSpace(RapidOcrDownloadDetail);

    /// <summary>
    /// 初始化 AI/BP 自动识别页面状态、下载事件、调试日志缓冲和后台刷新计时器。
    /// </summary>
    private void InitializeAiRecognition()
    {
        SelectedAiTestFrame = AiTestFrames[0];
        RecognitionStrategies =
        [
            new(SmartBpRecognitionStrategy.PureOcr, "SmartBpRecognitionStrategyPureOcr")
        ];
        SelectedRecognitionStrategy = RecognitionStrategies.FirstOrDefault(x => x.Strategy == _recognitionSettingsService.Settings.RecognitionStrategy)
                                      ?? RecognitionStrategies.FirstOrDefault();
        RefreshRecognitionEngineVisibility();
        QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusLoading");
        LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped");
        LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath;
        EnableAutoGuidanceSync = _recognitionSettingsService.Settings.EnableAutoGuidanceSync;
        EnableAutoApplyRecognition = _recognitionSettingsService.Settings.EnableAutoApplyRecognition;
        EnableAutoGuidancePageNavigation = _recognitionSettingsService.Settings.EnableAutoGuidancePageNavigation;
        EnableSmartBpProgressAutoCorrection = _recognitionSettingsService.Settings.EnableSmartBpProgressAutoCorrection;
        RecognitionApplyMode = _recognitionSettingsService.Settings.RecognitionApplyMode;
        AiOneStepDelayedMode = _recognitionSettingsService.Settings.AiOneStepDelayedMode;
        AiUnknownPhaseTalentInferenceFrames = _recognitionSettingsService.Settings.AiUnknownPhaseTalentInferenceFrames;
        PlayBackfillAnimations = _recognitionSettingsService.Settings.PlayBackfillAnimations;
        UseMultiImageSnapshotRequest = _recognitionSettingsService.Settings.UseMultiImageSnapshotRequest;
        EnableOcrBpRecognition = _recognitionSettingsService.Settings.EnableOcrBpRecognition;
        AiWithOcrFusionMode = _recognitionSettingsService.Settings.AiWithOcrFusionMode;
        AiWithAiOcrFusionMode = _recognitionSettingsService.Settings.AiWithAiOcrFusionMode;
        RecognitionIntervalMs = _recognitionSettingsService.Settings.RecognitionIntervalMs;
        OcrRecognitionIntervalMs = _recognitionSettingsService.Settings.OcrRecognitionIntervalMs;
        OcrFieldStaleMilliseconds = _recognitionSettingsService.Settings.OcrFieldStaleMilliseconds;
        OcrBackfillLookBehindSteps = _recognitionSettingsService.Settings.OcrBackfillLookBehindSteps;
        UseOcrContactSheet = _recognitionSettingsService.Settings.UseOcrContactSheet;
        EnableOcrDebugOverlay = _recognitionSettingsService.Settings.EnableOcrDebugOverlay;
        OcrProviders =
        [
            new(SmartBpOcrProviderMode.Paddle, string.Format(ResolveLocalizedOrRaw("SmartBpRecommendedProviderFormat"), "PaddleOCR")),
            new(SmartBpOcrProviderMode.Rapid, "RapidOCR"),
            new(SmartBpOcrProviderMode.Tesseract, "Tesseract OCR")
        ];
        SelectedOcrProvider = OcrProviders.First(item => item.Mode == _recognitionSettingsService.Settings.SelectedOcrProviderMode);
        TesseractLanguages = _recognitionSettingsService.Settings.TesseractLanguages;
        TesseractLanguageOptions = _tesseractDataAssetManager.GetAvailableLanguages()
            .Select(asset => new TesseractLanguageSelection(asset.Language, asset.DisplayNameKey))
            .ToArray();
        SyncSelectedTesseractLanguageOptions();
        EnableTesseractOcr = _recognitionSettingsService.Settings.EnableTesseractOcr;
        TesseractDefaultPsm = _recognitionSettingsService.Settings.TesseractDefaultPsm;
        TesseractMaxPreprocessVariants = _recognitionSettingsService.Settings.TesseractMaxPreprocessVariants;
        RapidOcrPadding = _recognitionSettingsService.Settings.RapidOcrPadding;
        RapidOcrMaxSideLen = _recognitionSettingsService.Settings.RapidOcrMaxSideLen;
        RapidOcrBoxScoreThreshold = _recognitionSettingsService.Settings.RapidOcrBoxScoreThreshold;
        RapidOcrBoxThreshold = _recognitionSettingsService.Settings.RapidOcrBoxThreshold;
        RapidOcrUnclipRatio = _recognitionSettingsService.Settings.RapidOcrUnclipRatio;
        RapidOcrUseAngleClassifier = _recognitionSettingsService.Settings.RapidOcrUseAngleClassifier;
        RapidOcrUsePreprocessingVariants = _recognitionSettingsService.Settings.RapidOcrUsePreprocessingVariants;
        RefreshOcrProviderStatuses();
        _ = RefreshTesseractDataStatusAsync();
        _ = InitializeRapidOcrAsync();
        AllowSequentialSnapshotFallback = _recognitionSettingsService.Settings.AllowSequentialSnapshotFallback;
        UseStrictCandidateEnumsInAutoSchema = _recognitionSettingsService.Settings.UseStrictCandidateEnumsInAutoSchema;
        PhaseCropMaxImageWidth = _recognitionSettingsService.Settings.PhaseCropMaxImageWidth;
        ContentCropMaxImageWidth = _recognitionSettingsService.Settings.ContentCropMaxImageWidth;
        PhaseMaxTokens = _recognitionSettingsService.Settings.PhaseMaxTokens;
        SnapshotDeltaMaxTokens = _recognitionSettingsService.Settings.SnapshotDeltaMaxTokens;
        PhaseTransitionCommitHoldMilliseconds = _recognitionSettingsService.Settings.PhaseTransitionCommitHoldMilliseconds;
        PhaseTransitionCommitHoldMaxMilliseconds = _recognitionSettingsService.Settings.PhaseTransitionCommitHoldMaxMilliseconds;
        RecognitionBackfillLookBehindSteps = _recognitionSettingsService.Settings.RecognitionBackfillLookBehindSteps;
        RecognitionFieldStaleMilliseconds = _recognitionSettingsService.Settings.RecognitionFieldStaleMilliseconds;
        RecognitionVisualBufferMilliseconds = _recognitionSettingsService.Settings.RecognitionVisualBufferMilliseconds;
        LlamaParallelSlots = _recognitionSettingsService.Settings.LlamaParallelSlots;
        LlamaGpuLayers = _recognitionSettingsService.Settings.LlamaGpuLayers;
        LlamaFlashAttention = _recognitionSettingsService.Settings.LlamaFlashAttention;
        LlamaBatchSize = _recognitionSettingsService.Settings.LlamaBatchSize;
        LlamaUBatchSize = _recognitionSettingsService.Settings.LlamaUBatchSize;
        RefreshRecognitionTimerInterval();
        RefreshRecognitionSpeedTestValidity();
        RefreshLlamaServerUiState();
        // 自动循环 Tick 只负责调度当前帧识别，具体阶段门禁和写回保护在 coordinator 内完成。
        _aiPreviewTimer.Tick += async (_, _) => await RunAutomaticCurrentFrameCoreAsync();
        _aiPerformanceTimer.Tick += async (_, _) => await RefreshAiPerformanceAsync();
        _aiPerformanceTimer.Start();
        _qwenAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            ApplyVisionModelDownloadState(state);
            if (!state.IsDownloading)
            {
                _ = RefreshSelectedQwenModelInstallStatusAsync();
                _ = RefreshSelectedAiOcrModelInstallStatusAsync();
                _activeVisionModelDownloadRole = null;
            }
        });
        _rapidOcrModelAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsRapidOcrDownloading = state.IsDownloading;
            RapidOcrDownloadProgress = state.Progress ?? 0;
            RapidOcrDownloadDetail = state.IsDownloading || !string.IsNullOrWhiteSpace(state.ErrorMessage)
                ? FormatDownloadState(state)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(state.ErrorMessage)) AiLastError = RapidOcrDownloadDetail;
            if (!state.IsDownloading) _ = RefreshRapidOcrStatusAsync();
        });
        _aiDebugLog.MessageWritten += (_, message) =>
        {
            lock (_debugLogBufferLock)
                _debugLogBuffer.AppendFormat("[{0:HH:mm:ss.fff}] [{1}] {2}{3}",
                    message.Timestamp, message.Source, message.Message, Environment.NewLine);
        };
        _debugLogFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _debugLogFlushTimer.Tick += (_, _) => FlushDebugLogBuffer();
        _debugLogFlushTimer.Start();
        _aiDebugLog.Write("SmartBP", "AI recognition diagnostics initialized.");
        _llamaRuntimeAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsLlamaRuntimeDownloading = state.IsDownloading;
            LlamaRuntimeDownloadProgress = state.Progress ?? 0;
            LlamaRuntimeDownloadStatus = ResolveLocalizedOrRaw(state.Status);
            LlamaRuntimeDownloadDetail = FormatDownloadState(state);
            if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
                AiLastError = LlamaRuntimeDownloadDetail;
            if (!state.IsDownloading)
                _ = RefreshLlamaRuntimeStatusAsync();
        });
        _tesseractDataAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsTesseractDataDownloading = state.IsDownloading;
            TesseractDownloadProgress = state.Progress ?? 0;
            TesseractDownloadDetail = FormatDownloadState(state);
            if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
                AiLastError = TesseractDownloadDetail;
            if (!state.IsDownloading) _ = RefreshTesseractDataStatusAsync();
        });
        _ = LoadLlamaCppAssetsAsync();
        _ = InitializeAiOptionsAsync();
        _ = LoadAiRegionProfileAsync();
        _ = RefreshQwenStatusAsync();
    }

    /// <summary>
    /// 从配置服务加载 BP 自动识别区域配置档。
    /// </summary>
    /// <returns>加载任务。</returns>
    private async Task LoadAiRegionProfileAsync()
    {
        try
        {
            _aiRegionProfile = await _aiRegionProfileService.LoadAsync();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    /// <summary>
    /// 打开 BP 自动识别区域编辑器，并保存用户覆盖配置。
    /// </summary>
    /// <returns>编辑流程完成后的任务。</returns>
    [RelayCommand]
    private async Task OpenAiRecognitionRegionEditorAsync()
    {
        try
        {
            _aiRegionProfile ??= await _aiRegionProfileService.LoadAsync();

            var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: false);
            if (frame == null)
                return;

            var editor = new RegionEditorWindow(frame, BuildAiRegionEditorLayout(_aiRegionProfile))
            {
                Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                        ?? Application.Current?.MainWindow
            };
            if (editor.ShowDialog() != true || editor.ResultLayout == null)
                return;

            ApplyAiRegionEditorLayout(_aiRegionProfile, editor.ResultLayout);
            await _aiRegionProfileService.SaveUserOverrideAsync(_aiRegionProfile);
            await LoadAiRegionProfileAsync();
            AiCropDebugInfo = ResolveLocalizedOrRaw("SmartBpAiRegionProfileSaved");
        }
        catch (Exception ex)
        {
            AiLastError = ex.Message;
            await MessageBoxHelper.ShowErrorAsync(FormatLocalizedDetail("SmartBpOperationFailedFormat", ex.Message));
        }
    }

    /// <summary>
    /// 将 BP 自动识别区域配置重置为模块内置默认值。
    /// </summary>
    /// <returns>重置流程完成后的任务。</returns>
    [RelayCommand]
    private async Task ResetAiRecognitionLayoutProfileAsync()
    {
        try
        {
            await _aiRegionProfileService.ResetUserOverrideAsync();
            await LoadAiRegionProfileAsync();
            AiCropDebugInfo = ResolveLocalizedOrRaw("SmartBpAiRegionProfileReset");
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    /// <summary>
    /// 将归一化 BP 识别区域配置档转换为通用区域编辑器使用的百分比布局。
    /// </summary>
    /// <param name="profile">BP 自动识别区域配置档。</param>
    /// <returns>区域编辑器布局。</returns>
    private RegionLayoutDefinition BuildAiRegionEditorLayout(SmartBpRecognitionLayoutProfile profile)
    {
        var layout = RegionLayoutDefinition.Builder(ResolveLocalizedOrRaw("SmartBpAiRegionEditor"));
        foreach (var (id, labelKey) in AiRegionEditorNodes)
        {
            if (!profile.Regions.TryGetValue(id, out var rect))
                throw new InvalidDataException($"Missing SmartBP AI recognition region: {id}.");

            layout.AddNode(
                id,
                ResolveLocalizedOrRaw(labelKey),
                new RegionNodeConfig
                {
                    Rect = new RelativeRect(rect.X * 100, rect.Y * 100, rect.Width * 100, rect.Height * 100),
                    ClampToParent = false
                });
        }

        return layout.Build();
    }

    /// <summary>
    /// 将区域编辑器保存的百分比布局写回归一化 BP 识别区域配置档。
    /// </summary>
    /// <param name="profile">待更新的 BP 自动识别区域配置档。</param>
    /// <param name="editedLayout">区域编辑器输出布局。</param>
    private static void ApplyAiRegionEditorLayout(
        SmartBpRecognitionLayoutProfile profile,
        RegionLayoutDefinition editedLayout)
    {
        var nodes = editedLayout.Roots.ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach (var (id, _) in AiRegionEditorNodes)
        {
            if (!nodes.TryGetValue(id, out var node))
                throw new InvalidDataException($"Edited SmartBP AI recognition layout is missing region: {id}.");

            profile.Regions[id] = new SmartBpRecognitionRegionRect
            {
                X = node.Rect.X / 100,
                Y = node.Rect.Y / 100,
                Width = node.Rect.W / 100,
                Height = node.Rect.H / 100
            };
        }
    }

    /// <summary>
    /// BP 自动识别区域编辑器中暴露给用户调整的区域节点。
    /// </summary>
    private static readonly (string Id, string LabelKey)[] AiRegionEditorNodes =
    [
        ("phase_top", "SmartBpAiRegionPhaseTop"),
        ("top_center_status", "SmartBpAiRegionTopCenterStatus"),
        ("top_left_status", "SmartBpAiRegionTopLeftStatus"),
        ("left_top", "SmartBpAiRegionLeftTop"),
        ("right_top", "SmartBpAiRegionRightTop"),
        ("left_bottom", "SmartBpAiRegionLeftBottom"),
        ("right_bottom", "SmartBpAiRegionRightBottom")
    ];

    private async Task InitializeAiOptionsAsync()
    {
        try
        {
            AiPromptProfiles = await _promptProfileProvider.GetAvailableProfilesAsync();
            SelectedAiPromptProfile = AiPromptProfiles.FirstOrDefault(x => x.Id == _recognitionSettingsService.Settings.PromptProfileId) ?? AiPromptProfiles.FirstOrDefault();
            QwenModelProfiles = await _qwenAssetManager.GetProfilesAsync();
            SelectedQwenModelProfile = QwenModelProfiles.FirstOrDefault(x => x.Id == _recognitionSettingsService.Settings.SelectedBusinessAiModelId) ??
                                       QwenModelProfiles.FirstOrDefault(profile => profile.Role is LocalVisionModelRole.BusinessVlm or LocalVisionModelRole.Both) ??
                                       QwenModelProfiles.FirstOrDefault();
            AiOcrModelProfiles = QwenModelProfiles
                .Where(profile => profile.Role is LocalVisionModelRole.AiOcrTextExtractor or LocalVisionModelRole.Both)
                .ToArray();
            SelectedAiOcrModelProfile = AiOcrModelProfiles.FirstOrDefault(x => x.Id == _recognitionSettingsService.Settings.SelectedAiOcrModelId) ??
                                        AiOcrModelProfiles.FirstOrDefault();
            await RefreshSelectedQwenModelInstallStatusAsync();
            await RefreshSelectedAiOcrModelInstallStatusAsync();
            // llama.cpp 资产通常已在 InitializeAiRecognition 中提前加载；这里兜底处理加载失败的场景。
            if (LlamaRuntimeAssets.Count == 0)
            {
                var assets = await _llamaRuntimeAssetManager.GetAvailableAssetsAsync();
                var selections = assets.Select(a => new LlamaCppRuntimeAssetSelection(a)).ToList();
                await RefreshLlamaRuntimeAssetsInstallStatusAsync(selections);
                LlamaRuntimeAssets = selections;
                var selected = await _llamaRuntimeAssetManager.GetSelectedAssetAsync();
                SelectedLlamaRuntimeAsset = selections.FirstOrDefault(s => s.Id == selected.Id) ?? selections.FirstOrDefault();
                await RefreshLlamaRuntimeStatusAsync();
            }
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    /// <summary>异步加载内置 llama.cpp 运行时资产，避免阻塞 UI 线程。</summary>
    private async Task LoadLlamaCppAssetsAsync()
    {
        LlamaRuntimeDownloadStatus = ResolveLocalizedOrRaw("SmartBpAiStatusLoading");
        try
        {
            var assets = await _llamaRuntimeAssetManager.GetAvailableAssetsAsync();
            var selections = assets.Select(a => new LlamaCppRuntimeAssetSelection(a)).ToList();
            foreach (var selection in selections)
                selection.IsInstalled = await _llamaRuntimeAssetManager.IsAssetInstalledAsync(selection.Id, selection.EntryExe);
            var selected = await _llamaRuntimeAssetManager.GetSelectedAssetAsync();
            var installed = await _llamaRuntimeAssetManager.IsInstalledAsync();
            var executable = installed ? await _llamaRuntimeAssetManager.GetInstalledExecutablePathAsync() : "-";
            RunOnUiThread(() =>
            {
                LlamaRuntimeAssets = selections;
                SelectedLlamaRuntimeAsset = selections.FirstOrDefault(s => s.Id == selected.Id) ?? selections.FirstOrDefault();
                IsLlamaRuntimeInstalled = installed;
                ManagedLlamaServerExecutablePath = executable;
                LlamaRuntimeDownloadStatus = installed
                    ? ResolveLocalizedOrRaw("SmartBpAiStatusInstalled")
                    : ResolveLocalizedOrRaw("SmartBpAiStatusNotInstalled");
                RefreshLlamaServerUiState();
            });
        }
        catch (Exception ex)
        {
            _aiDebugLog.Write("runtime", $"llama.cpp asset load failed, will retry later: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearAiDebugLog()
    {
        lock (_debugLogBufferLock)
            _debugLogBuffer.Clear();
        AiDebugLogText = "";
    }

    /// <summary>将缓冲的日志消息写入 <see cref="AiDebugLogText"/> 并清空缓冲区。</summary>
    private void FlushDebugLogBuffer()
    {
        string batch;
        lock (_debugLogBufferLock)
        {
            if (_debugLogBuffer.Length == 0) return;
            batch = _debugLogBuffer.ToString();
            _debugLogBuffer.Clear();
        }

        const int maximumCharacters = 60000;
        var newText = AiDebugLogText + batch;
        if (newText.Length > maximumCharacters)
        {
            var firstLineBreak = newText.IndexOf(Environment.NewLine, newText.Length - maximumCharacters, StringComparison.Ordinal);
            newText = firstLineBreak >= 0 ? newText[(firstLineBreak + Environment.NewLine.Length)..] : newText[^maximumCharacters..];
        }
        AiDebugLogText = newText;
    }

    partial void OnIsDebugLogEnabledChanged(bool value)
    {
        _aiDebugLog.IsEnabled = value;
    }

    [RelayCommand] private async Task RefreshQwenStatusAsync()
    {
        try
        {
            var p = await _qwenAssetManager.GetProfileAsync();
            QwenModelProfile = p.DisplayName;
            CurrentQwenModelDisplayName = string.Format(ResolveLocalizedOrRaw("SmartBpCurrentQwenModelFormat"), p.DisplayName);
            QwenMmprojProfile = Path.GetFileNameWithoutExtension(p.MmprojFileName);
            SelectedQwenModelProfile = QwenModelProfiles.FirstOrDefault(x => x.Id == p.Id) ?? SelectedQwenModelProfile;
            IsQwenInstalled = await _qwenAssetManager.IsInstalledAsync();
            await RefreshSelectedQwenModelInstallStatusAsync();
            QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusLoaded");
            SwitchSelectedQwenModelCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed"); AiLastError = ex.Message; }
    }

    private void RefreshRecognitionEngineVisibility()
    {
        IsOcrRecognitionEngine = true;
        IsAiQwenRecognitionEngine = false;
        IsBusinessAiModelVisible = false;
        IsOcrProviderCardVisible = true;
        IsAiOcrModelVisible = false;
        IsAiWithOcrFusionModeVisible = false;
        IsAiWithAiOcrFusionModeVisible = false;
        var provider = SelectedOcrProvider?.Mode ?? _recognitionSettingsService.Settings.SelectedOcrProviderMode;
        IsPaddleRecognitionEngine = IsOcrProviderCardVisible && provider == SmartBpOcrProviderMode.Paddle;
        IsTesseractRecognitionEngine = IsOcrProviderCardVisible && provider == SmartBpOcrProviderMode.Tesseract;
        IsRapidRecognitionEngine = IsOcrProviderCardVisible && provider == SmartBpOcrProviderMode.Rapid;
        RefreshRecognitionTimerInterval();
    }

    private void RefreshRecognitionTimerInterval()
    {
        var interval = _recognitionSettingsService.Settings.OcrRecognitionIntervalMs;
        _aiPreviewTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(interval, 100, 5000));
    }

    private async Task RefreshAiPerformanceAsync()
    {
        if (!IsAiQwenRecognitionEngine && !_llamaServerManager.IsRunning) return;
        var snapshot = await _aiPerformanceMonitor.GetSnapshotAsync(_llamaServerManager.ProcessId);
        AiGpuName = snapshot.GpuName;
        AiGpuUtilization = snapshot.GpuUtilizationPercent is { } utilization ? $"{utilization}%" : "not available";
        AiVramUsage = snapshot.VramUsedBytes is { } used && snapshot.VramTotalBytes is { } total
            ? $"{FormatBytes((long)used)} / {FormatBytes((long)total)}" : "not available";
        AiLlamaProcessId = snapshot.ProcessId?.ToString() ?? "-";
        AiPerformanceUpdatedAt = snapshot.UpdatedAt.ToString("HH:mm:ss");
    }

    [RelayCommand]
    private async Task TestRecognitionSpeedAsync()
    {
        if (IsRecognitionSpeedTesting) return;
        IsRecognitionSpeedTesting = true;
        try
        {
            var elapsed = new List<long>();
            var testFrame = SelectedAiTestFrame ?? AiTestFrames.FirstOrDefault();
            if (testFrame == null) throw new InvalidOperationException("No SmartBP recognition test frame is available.");
            var frame = LoadTestFrame(testFrame);
            var watch = Stopwatch.StartNew();
            await _ocrBpRecognitionService.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(
            [
                SmartBpRecognitionRegion.RightTop, SmartBpRecognitionRegion.LeftTop,
                SmartBpRecognitionRegion.LeftBottom, SmartBpRecognitionRegion.RightBottom
            ]));
            watch.Stop();
            elapsed.Add(watch.ElapsedMilliseconds);
            var minimum = checked((int)Math.Min(int.MaxValue, elapsed.Max() + 250));
            var settings = _recognitionSettingsService.Settings;
            settings.MinimumOcrRecognitionIntervalMs = minimum;
            settings.OcrRecognitionIntervalMs = Math.Max(settings.OcrRecognitionIntervalMs, minimum);
            OcrRecognitionIntervalMs = settings.OcrRecognitionIntervalMs;
            settings.LastRecognitionSpeedTestAt = DateTimeOffset.Now;
            settings.LastRecognitionSpeedTestEngine = GetRecognitionSpeedFingerprint();
            settings.LastRecognitionSpeedTestConfigurationHash = settings.LastRecognitionSpeedTestEngine;
            await _recognitionSettingsService.SaveAsync();
            RecognitionSpeedTestStatus = $"max={elapsed.Max()} ms; minimum={minimum} ms";
            RefreshRecognitionSpeedTestValidity();
        }
        catch (Exception ex) { RecognitionSpeedTestStatus = ex.Message; }
        finally { IsRecognitionSpeedTesting = false; }
    }

    private string GetRecognitionSpeedFingerprint()
    {
        var s = _recognitionSettingsService.Settings;
        return $"{s.RecognitionStrategy}|{s.SelectedOcrProviderMode}|{s.UseOcrContactSheet}|{s.TesseractLanguages}|{s.TesseractDefaultPsm}|{s.TesseractMaxPreprocessVariants}|{s.SelectedRapidOcrModelId}|{s.RapidOcrPadding}|{s.RapidOcrMaxSideLen}|{s.RapidOcrBoxScoreThreshold}|{s.RapidOcrBoxThreshold}|{s.RapidOcrUnclipRatio}|{s.RapidOcrUseAngleClassifier}|{s.RapidOcrUsePreprocessingVariants}";
    }

    private void RefreshRecognitionSpeedTestValidity()
    {
        var settings = _recognitionSettingsService.Settings;
        IsRecognitionIntervalEditable = string.Equals(
            settings.LastRecognitionSpeedTestConfigurationHash,
            GetRecognitionSpeedFingerprint(), StringComparison.Ordinal);
        CurrentRecognitionEngineText = settings.RecognitionStrategy.ToString();
        CurrentRecognitionIntervalMs = settings.OcrRecognitionIntervalMs;
        MinimumRecognitionIntervalMs = settings.MinimumOcrRecognitionIntervalMs;
        RecognitionIntervalEditHint = IsRecognitionIntervalEditable
            ? ResolveLocalizedOrRaw("SmartBpRecognitionIntervalReady")
            : ResolveLocalizedOrRaw("SmartBpRecognitionIntervalRequiresSpeedTest");
    }
    [RelayCommand(CanExecute = nameof(CanDownloadQwenModel))]
    private async Task DownloadQwenModelAsync()
    {
        if (SelectedQwenModelProfile == null) return;
        try
        {
            _activeVisionModelDownloadRole = LocalVisionModelDownloadRole.BusinessAi;
            await _qwenAssetManager.InstallAsync(SelectedQwenModelProfile.Id);
            await RefreshQwenStatusAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AiLastError = ex.ToString(); }
    }

    [RelayCommand] private void CancelQwenDownload() => _qwenAssetManager.Cancel();
    [RelayCommand] private void CancelBusinessAiModelDownload() => _qwenAssetManager.Cancel();
    [RelayCommand] private void CancelAiOcrModelDownload() => _qwenAssetManager.Cancel();

    [RelayCommand(CanExecute = nameof(CanDeleteQwenModel))]
    private async Task DeleteQwenModelAsync()
    {
        if (SelectedQwenModelProfile == null) return;
        try
        {
            if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before deleting the model.");
            await _qwenAssetManager.DeleteAsync(SelectedQwenModelProfile.Id);
            await RefreshQwenStatusAsync();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    private bool CanDownloadQwenModel() =>
        !IsBusinessAiModelDownloading && !IsAiOcrModelDownloading && SelectedQwenModelProfile != null && !IsSelectedQwenModelInstalled;

    private bool CanDeleteQwenModel() =>
        !IsBusinessAiModelDownloading && !IsAiOcrModelDownloading && !_llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi).IsRunning && SelectedQwenModelProfile != null && IsSelectedQwenModelInstalled;

    [RelayCommand(CanExecute = nameof(CanDownloadAiOcrModel))]
    private async Task DownloadAiOcrModelAsync()
    {
        if (SelectedAiOcrModelProfile == null) return;
        try
        {
            _activeVisionModelDownloadRole = LocalVisionModelDownloadRole.AiOcr;
            await _qwenAssetManager.InstallAsync(SelectedAiOcrModelProfile.Id);
            await RefreshSelectedAiOcrModelInstallStatusAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AiLastError = ex.ToString(); }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteAiOcrModel))]
    private async Task DeleteAiOcrModelAsync()
    {
        if (SelectedAiOcrModelProfile == null) return;
        try
        {
            if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before deleting the model.");
            await _qwenAssetManager.DeleteAsync(SelectedAiOcrModelProfile.Id);
            await RefreshSelectedAiOcrModelInstallStatusAsync();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanSwitchSelectedAiOcrModel))]
    private async Task SwitchSelectedAiOcrModelAsync()
    {
        if (SelectedAiOcrModelProfile == null) return;
        await SwitchAiOcrModelAsync(SelectedAiOcrModelProfile);
    }

    private bool CanDownloadAiOcrModel() =>
        !IsAiOcrModelDownloading && !IsBusinessAiModelDownloading && SelectedAiOcrModelProfile != null && !IsSelectedAiOcrModelInstalled;

    private bool CanDeleteAiOcrModel() =>
        !IsAiOcrModelDownloading && !IsBusinessAiModelDownloading && !IsAiOcrRelevantServerRunning() && SelectedAiOcrModelProfile != null && IsSelectedAiOcrModelInstalled;

    private bool CanSwitchSelectedAiOcrModel() =>
        !IsAiOcrModelDownloading && !IsBusinessAiModelDownloading &&
        SelectedAiOcrModelProfile != null &&
        !string.Equals(SelectedAiOcrModelProfile.Id, _recognitionSettingsService.Settings.SelectedAiOcrModelId, StringComparison.Ordinal);

    private bool CanDownloadLlamaRuntime() =>
        !IsLlamaRuntimeDownloading && !IsLlamaServerRunning && !IsLlamaServerStarting && SelectedLlamaRuntimeAsset != null && !SelectedLlamaRuntimeAsset.IsInstalled;

    private bool CanDeleteLlamaRuntime() =>
        !IsLlamaRuntimeDownloading && !IsLlamaServerRunning && !IsLlamaServerStarting && SelectedLlamaRuntimeAsset != null && SelectedLlamaRuntimeAsset.IsInstalled;

    private bool CanRollbackLlamaRuntime() =>
        !IsLlamaRuntimeDownloading && !IsLlamaServerRunning && !IsLlamaServerStarting && IsLlamaRuntimeInstalled;

    private bool CanStartLlamaServer() =>
        !IsLlamaServerRunning && !IsLlamaServerStarting && IsLlamaRuntimeInstalled;

    private bool CanStopLlamaServer() =>
        IsLlamaServerRunning;

    private bool CanForceStopLlamaServer() => true;
    [RelayCommand] private async Task BrowseLlamaServerAsync() { var path = _filePickerService.PickExecutableFile(); if (path == null) return; LlamaServerExecutablePath = path; _recognitionSettingsService.Settings.LlamaServerExecutablePath = path; await _recognitionSettingsService.SaveAsync(); }
    /// <inheritdoc cref="CanDownloadLlamaRuntime"/>
    [RelayCommand(CanExecute = nameof(CanDownloadLlamaRuntime))]
    private async Task DownloadLlamaRuntimeAsync() { try { if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before installing or updating the runtime."); await _llamaRuntimeAssetManager.InstallAsync(); LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath; await RefreshLlamaRuntimeStatusAsync(); } catch (OperationCanceledException) { } catch (Exception ex) { AiLastError = ex.ToString(); } }
    [RelayCommand] private void CancelLlamaRuntimeDownload() => _llamaRuntimeAssetManager.Cancel();
    /// <inheritdoc cref="CanDeleteLlamaRuntime"/>
    [RelayCommand(CanExecute = nameof(CanDeleteLlamaRuntime))]
    private async Task DeleteLlamaRuntimeAsync() { try { if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before deleting the runtime."); await _llamaRuntimeAssetManager.DeleteAsync(); LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath; await RefreshLlamaRuntimeStatusAsync(); } catch (Exception ex) { AiLastError = ex.Message; } }
    /// <inheritdoc cref="CanRollbackLlamaRuntime"/>
    [RelayCommand(CanExecute = nameof(CanRollbackLlamaRuntime))]
    private async Task RollbackLlamaRuntimeAsync() { try { if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before rolling back the runtime."); await _llamaRuntimeAssetManager.RollbackAsync(); LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath; await RefreshLlamaRuntimeStatusAsync(); } catch (Exception ex) { AiLastError = ex.Message; } }
    [RelayCommand] private async Task RefreshLlamaRuntimeStatusAsync()
    {
        if (LlamaRuntimeAssets.Count > 0)
            await RefreshLlamaRuntimeAssetsInstallStatusAsync(LlamaRuntimeAssets);
        IsLlamaRuntimeInstalled = await _llamaRuntimeAssetManager.IsInstalledAsync();
        ManagedLlamaServerExecutablePath = IsLlamaRuntimeInstalled ? await _llamaRuntimeAssetManager.GetInstalledExecutablePathAsync() : "-";
        DownloadLlamaRuntimeCommand.NotifyCanExecuteChanged();
        DeleteLlamaRuntimeCommand.NotifyCanExecuteChanged();
        RollbackLlamaRuntimeCommand.NotifyCanExecuteChanged();
        StartLlamaServerCommand.NotifyCanExecuteChanged();
        StopLlamaServerCommand.NotifyCanExecuteChanged();
        RefreshLlamaServerUiState();
    }

    private async Task RefreshLlamaRuntimeAssetsInstallStatusAsync(IReadOnlyList<LlamaCppRuntimeAssetSelection> selections)
    {
        foreach (var selection in selections)
            selection.IsInstalled = await _llamaRuntimeAssetManager.IsAssetInstalledAsync(selection.Id, selection.EntryExe);
    }
    [RelayCommand] private async Task CheckLlamaRuntimeUpdateAsync() { try { var result = await _llamaRuntimeUpdateService.CheckForUpdatesAsync(true); LlamaRuntimeUpdateStatus = $"{result.Message} Current={result.CurrentVersion}; Latest={result.LatestVersion ?? "-"}"; if (result.LatestAssets.Count > 0) { var selections = result.LatestAssets.Select(a => new LlamaCppRuntimeAssetSelection(a)).ToList(); await RefreshLlamaRuntimeAssetsInstallStatusAsync(selections); LlamaRuntimeAssets = selections; } } catch (Exception ex) { LlamaRuntimeUpdateStatus = ex.Message; } }
    [RelayCommand(CanExecute = nameof(CanStartLlamaServer))]
    private async Task StartLlamaServerAsync()
    {
        IsLlamaServerStarting = true;
        LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStarting");
        try
        {
            await _llamaServerManager.StartAsync();
            LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusReady");
            IsLlamaServerRunning = true;
        }
        catch (Exception ex)
        {
            LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed");
            IsLlamaServerRunning = false;
            AiLastError = ex.Message;
        }
        finally
        {
            IsLlamaServerStarting = false;
        }
    }
    [RelayCommand(CanExecute = nameof(CanStopLlamaServer))]
    private async Task StopLlamaServerAsync() { await StopAiPreviewLoopAsync(); await _llamaServerManager.StopAsync(); LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped"); IsLlamaServerRunning = false; }
    [RelayCommand(CanExecute = nameof(CanForceStopLlamaServer))]
    private async Task ForceStopLlamaServerAsync() { try { await StopAiPreviewLoopAsync(); await _llamaServerManager.ForceStopManagedProcessAsync(); LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped"); IsLlamaServerRunning = false; } catch (Exception ex) { AiLastError = ex.Message; } }

    [RelayCommand(CanExecute = nameof(CanStartBusinessAiServer))]
    private async Task StartBusinessAiServerAsync() => await StartRoleServerAsync(LlamaVisionServerRole.BusinessAi);

    [RelayCommand(CanExecute = nameof(CanStopBusinessAiServer))]
    private async Task StopBusinessAiServerAsync() => await StopRoleServerAsync(LlamaVisionServerRole.BusinessAi);

    [RelayCommand(CanExecute = nameof(CanForceStopBusinessAiServer))]
    private async Task ForceStopBusinessAiServerAsync() => await ForceStopRoleServerAsync(LlamaVisionServerRole.BusinessAi);

    [RelayCommand(CanExecute = nameof(CanStartAiOcrServer))]
    private async Task StartAiOcrServerAsync() => await StartRoleServerAsync(LlamaVisionServerRole.AiOcr);

    [RelayCommand(CanExecute = nameof(CanStopAiOcrServer))]
    private async Task StopAiOcrServerAsync() => await StopRoleServerAsync(LlamaVisionServerRole.AiOcr);

    [RelayCommand(CanExecute = nameof(CanForceStopAiOcrServer))]
    private async Task ForceStopAiOcrServerAsync() => await ForceStopRoleServerAsync(LlamaVisionServerRole.AiOcr);

    [RelayCommand(CanExecute = nameof(CanStartRequiredLlamaServers))]
    private async Task StartRequiredLlamaServersAsync()
    {
        IsRequiredLlamaServersStarting = true;
        try
        {
            await Task.CompletedTask;
        }
        finally
        {
            IsRequiredLlamaServersStarting = false;
            NotifyRoleServerCommands();
        }
    }

    [RelayCommand]
    private async Task StopAllSmartBpLlamaServersAsync()
    {
        await StopRoleServerAsync(LlamaVisionServerRole.AiOcr);
        await StopRoleServerAsync(LlamaVisionServerRole.BusinessAi);
    }

    private bool CanStartBusinessAiServer() => IsLlamaRuntimeInstalled && !IsBusinessAiServerStarting && !_llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi).IsRunning;
    private bool CanStopBusinessAiServer() => _llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi).IsRunning;
    private bool CanForceStopBusinessAiServer() => true;
    private bool CanStartAiOcrServer() => IsLlamaRuntimeInstalled && !IsAiOcrServerStarting && !IsAiOcrReusingBusinessServer() && !_llamaServerManagers.Get(LlamaVisionServerRole.AiOcr).IsRunning;
    private bool CanStopAiOcrServer() => !IsAiOcrReusingBusinessServer() && _llamaServerManagers.Get(LlamaVisionServerRole.AiOcr).IsRunning;
    private bool CanForceStopAiOcrServer() => !IsAiOcrReusingBusinessServer();
    private bool CanStartRequiredLlamaServers() => false;

    private async Task StartRoleServerAsync(LlamaVisionServerRole role)
    {
        SetRoleServerStarting(role, true);
        try
        {
            await _llamaServerManagers.Get(role).StartAsync();
            RefreshRoleServerStatus();
        }
        catch (Exception ex)
        {
            SetRoleServerFailed(role);
            AiLastError = ex.Message;
        }
        finally
        {
            SetRoleServerStarting(role, false);
            NotifyRoleServerCommands();
        }
    }

    private async Task StopRoleServerAsync(LlamaVisionServerRole role)
    {
        try
        {
            await _llamaServerManagers.Get(role).StopAsync();
            RefreshRoleServerStatus();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
        finally { NotifyRoleServerCommands(); }
    }

    private async Task ForceStopRoleServerAsync(LlamaVisionServerRole role)
    {
        try
        {
            await _llamaServerManagers.Get(role).ForceStopManagedProcessAsync();
            RefreshRoleServerStatus();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
        finally { NotifyRoleServerCommands(); }
    }

    private async Task ReconcileLlamaServersForCurrentStrategyAsync()
    {
        try
        {
            var business = _llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi);
            var aiOcr = _llamaServerManagers.Get(LlamaVisionServerRole.AiOcr);
            switch (_recognitionSettingsService.Settings.RecognitionStrategy)
            {
                case SmartBpRecognitionStrategy.PureOcr:
                    if (aiOcr.IsRunning) await aiOcr.StopAsync();
                    if (business.IsRunning) await business.StopAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            AiLastError = ex.Message;
        }
        finally
        {
            RefreshLlamaServerUiState();
        }
    }

    private void SetRoleServerStarting(LlamaVisionServerRole role, bool value, bool isRestart = false)
    {
        if (role == LlamaVisionServerRole.BusinessAi)
        {
            IsBusinessAiServerStarting = value;
            if (value)
            {
                BusinessAiServerStatus = ResolveLocalizedOrRaw(isRestart ? "SmartBpAiStatusRestarting" : "SmartBpAiStatusStarting");
                BusinessAiServerActivityText = ResolveLocalizedOrRaw(isRestart ? "SmartBpBusinessAiServerRestarting" : "SmartBpBusinessAiServerStarting");
            }
        }
        else
        {
            IsAiOcrServerStarting = value;
            if (value)
            {
                AiOcrServerStatus = ResolveLocalizedOrRaw(isRestart ? "SmartBpAiStatusRestarting" : "SmartBpAiStatusStarting");
                AiOcrServerActivityText = ResolveLocalizedOrRaw(isRestart ? "SmartBpAiOcrServerRestarting" : "SmartBpAiOcrServerStarting");
            }
        }
    }

    private void SetRoleServerFailed(LlamaVisionServerRole role)
    {
        if (role == LlamaVisionServerRole.BusinessAi)
            BusinessAiServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed");
        else
            AiOcrServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed");
    }

    private void RefreshRoleServerStatus()
    {
        var business = _llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi);
        var aiOcr = _llamaServerManagers.Get(LlamaVisionServerRole.AiOcr);
        BusinessAiServerStatus = business.Status;
        BusinessAiServerProcessId = business.ProcessId?.ToString() ?? "-";
        BusinessAiServerPortText = business.Port.ToString();
        BusinessAiServerModelText = _recognitionSettingsService.Settings.SelectedBusinessAiModelId;
        AiOcrServerStatus = IsAiOcrReusingBusinessServer() ? "Reusing Business AI server" : aiOcr.Status;
        AiOcrServerProcessId = IsAiOcrReusingBusinessServer() ? business.ProcessId?.ToString() ?? "-" : aiOcr.ProcessId?.ToString() ?? "-";
        AiOcrServerPortText = IsAiOcrReusingBusinessServer() ? business.Port.ToString() : aiOcr.Port.ToString();
        AiOcrServerModelText = _recognitionSettingsService.Settings.SelectedAiOcrModelId;
        AiOcrServerReuseStatus = IsAiOcrReusingBusinessServer()
            ? "AI OCR is reusing the Business AI server. No separate AI OCR server is required."
            : "AI OCR uses a separate role-specific llama.cpp server.";
        DebugServerStatus = FormatRoleServerStatus();
    }

    private void RefreshLlamaServerUiState()
    {
        IsLlamaServerRunning = _llamaServerManager.IsRunning;
        LlamaServerStatus = _llamaServerManager.Status;
        RefreshRoleServerStatus();
        DownloadLlamaRuntimeCommand.NotifyCanExecuteChanged();
        DeleteLlamaRuntimeCommand.NotifyCanExecuteChanged();
        RollbackLlamaRuntimeCommand.NotifyCanExecuteChanged();
        StartLlamaServerCommand.NotifyCanExecuteChanged();
        StopLlamaServerCommand.NotifyCanExecuteChanged();
        ForceStopLlamaServerCommand.NotifyCanExecuteChanged();
        NotifyRoleServerCommands();
    }

    private bool IsAiOcrReusingBusinessServer() =>
        !_recognitionSettingsService.Settings.UseSeparateAiOcrServer ||
        string.Equals(_recognitionSettingsService.Settings.SelectedBusinessAiModelId, _recognitionSettingsService.Settings.SelectedAiOcrModelId, StringComparison.Ordinal);

    private bool IsAiOcrRelevantServerRunning() =>
        IsAiOcrReusingBusinessServer()
            ? _llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi).IsRunning
            : _llamaServerManagers.Get(LlamaVisionServerRole.AiOcr).IsRunning;

    private string FormatRoleServerStatus() =>
        $"BusinessAi: model={_recognitionSettingsService.Settings.SelectedBusinessAiModelId}; port={_llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi).Port}; status={BusinessAiServerStatus}; pid={BusinessAiServerProcessId}{Environment.NewLine}" +
        $"AiOcr: model={_recognitionSettingsService.Settings.SelectedAiOcrModelId}; port={AiOcrServerPortText}; status={AiOcrServerStatus}; pid={AiOcrServerProcessId}; reuse={IsAiOcrReusingBusinessServer()}";

    private void NotifyRoleServerCommands()
    {
        StartBusinessAiServerCommand.NotifyCanExecuteChanged();
        StopBusinessAiServerCommand.NotifyCanExecuteChanged();
        ForceStopBusinessAiServerCommand.NotifyCanExecuteChanged();
        StartAiOcrServerCommand.NotifyCanExecuteChanged();
        StopAiOcrServerCommand.NotifyCanExecuteChanged();
        ForceStopAiOcrServerCommand.NotifyCanExecuteChanged();
        StartRequiredLlamaServersCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand] private async Task RecognizeSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try
        {
            var frame = LoadTestFrame(SelectedAiTestFrame);
            await RunFullStrategyRecognitionCoreAsync(frame);
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private Task RecognizeCurrentCaptureFrameAsync() => RecognizeCurrentFrameCoreAsync();
    [RelayCommand]
    private async Task ForceSyncGameProgressAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true, useInfoBar: true);
        if (frame == null) return;
        try
        {
            IsAiRecognizing = true;
            var snapshot = await _autoRecognitionCoordinator.RecognizeFullBpSnapshotAsync(frame, mergeIntoStateStore: true);
            if (snapshot.BusinessState == null)
            {
                var error = LocalizeProgressSyncMessage(snapshot.Error) ?? ResolveLocalizedOrRaw("SmartBpProgressForceSyncNoSnapshot");
                LastSmartBpProgressDiagnosis = error;
                _infoBarService.ShowWarningInfoBar(error);
                return;
            }

            var result = await _progressSyncService.ForceSyncAsync(snapshot.BusinessState, SmartBpProgressSyncMode.Manual);
            ApplyRegionGatedResult(snapshot);
            LastSmartBpProgressDiagnosis = FormatProgressSyncResult(result);
            AiCandidateOperations = string.Join(Environment.NewLine, result.Diagnostics);
            AiNormalizedResult = AiCandidateOperations;
            ShowForceSyncProgressInfoBar(result);
        }
        catch (OperationCanceledException)
        {
            var message = ResolveLocalizedOrRaw("QueueCanceled");
            LastSmartBpProgressDiagnosis = message;
            _infoBarService.ShowInformationalInfoBar(message);
        }
        catch (Exception ex)
        {
            AiLastError = ex.ToString();
            var message = FormatLocalizedDetail("SmartBpProgressForceSyncFailedFormat", ex.Message);
            LastSmartBpProgressDiagnosis = message;
            _infoBarService.ShowErrorInfoBar(message);
        }
        finally
        {
            IsAiRecognizing = false;
        }
    }

    [RelayCommand] private async Task RecognizeIncrementalSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try { await RunIncrementalRecognitionCoreAsync(LoadTestFrame(SelectedAiTestFrame)); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private async Task RecognizeIncrementalCurrentCaptureFrameAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true);
        if (frame == null) return;
        await RunIncrementalRecognitionCoreAsync(frame);
    }
    [RelayCommand] private async Task DetectStageFromSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try { await RunPhaseOnlyRecognitionCoreAsync(LoadTestFrame(SelectedAiTestFrame)); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private async Task DetectStageFromCurrentCaptureFrameAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true);
        if (frame == null) return;
        await RunPhaseOnlyRecognitionCoreAsync(frame);
    }
    [RelayCommand] private Task RunAutomaticOneTickAsync() => RunAutomaticCurrentFrameCoreAsync();
    /// <summary>
    /// 启动自动识别循环，并在需要时启动对应 llama.cpp 服务。
    /// </summary>
    /// <returns>启动流程完成后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanStartAutomaticRecognition))]
    private async Task StartAiPreviewLoopAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true);
        if (frame == null) { NotifyAutomaticRecognitionCommands(); return; }

        var confirmed = await MessageBoxHelper.ShowConfirmAsync(
            ResolveLocalizedOrRaw("SmartBpAutoRecognitionStartConfirm"),
            ResolveLocalizedOrRaw("SmartBpAutoRecognitionStartTitle"),
            ResolveLocalizedOrRaw("Confirm"), ResolveLocalizedOrRaw("Cancel"));
        if (!confirmed) return;
        try
        {
            await EnsureRequiredLlamaServersForAutomaticRecognitionAsync();
            await _autoRecognitionCoordinator.StartAsync();
            _isAutomaticRecognitionStopPendingAfterQueueDrain = false;
            _automaticRecognitionUnavailableFrameCount = 0;
            IsAiPreviewLoopRunning = true;
            _aiPreviewTimer.Start();
            _autoRecognitionGlobalControl.Update(true, _ => StopAiPreviewLoopAsync());
        }
        catch (Exception ex)
        {
            _aiPreviewTimer.Stop();
            IsAiPreviewLoopRunning = false;
            _autoRecognitionGlobalControl.Update(false);
            AiLastError = ex.ToString();
        }
        finally
        {
            NotifyAutomaticRecognitionCommands();
        }
    }

    /// <summary>
    /// 停止自动识别循环并清理全局停止控制入口。
    /// </summary>
    /// <returns>停止流程完成后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanStopAutomaticRecognition))]
    private async Task StopAiPreviewLoopAsync()
    {
        _isAutomaticRecognitionStopPendingAfterQueueDrain = false;
        _automaticRecognitionUnavailableFrameCount = 0;
        _aiPreviewTimer.Stop();
        await _autoRecognitionCoordinator.StopAsync();
        IsAiPreviewLoopRunning = false;
        _autoRecognitionGlobalControl.Update(false);
        NotifyAutomaticRecognitionCommands();
    }

    /// <summary>
    /// 判断当前是否允许启动自动识别循环。
    /// </summary>
    private bool CanStartAutomaticRecognition() => !IsAiPreviewLoopRunning && !IsAiRecognizing;

    /// <summary>
    /// 判断当前是否允许停止自动识别循环。
    /// </summary>
    private bool CanStopAutomaticRecognition() => IsAiPreviewLoopRunning || IsAiRecognizing;

    /// <summary>
    /// 按当前识别策略确保自动识别所需的 llama.cpp 服务已经启动。
    /// </summary>
    /// <returns>服务启动和校验任务。</returns>
    /// <exception cref="InvalidOperationException">运行时未安装或必要服务启动失败时抛出。</exception>
    private async Task EnsureRequiredLlamaServersForAutomaticRecognitionAsync()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// 刷新自动识别启动/停止命令状态。
    /// </summary>
    private void NotifyAutomaticRecognitionCommands()
    {
        StartAiPreviewLoopCommand.NotifyCanExecuteChanged();
        StopAiPreviewLoopCommand.NotifyCanExecuteChanged();
        StopCaptureCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 下载当前勾选的 Tesseract traineddata 语言文件。
    /// </summary>
    /// <returns>下载任务。</returns>
    [RelayCommand]
    private async Task DownloadTesseractDataAsync()
    {
        try
        {
            var languages = GetSelectedTesseractLanguages();
            if (languages.Length == 0) { AiLastError = ResolveLocalizedOrRaw("SmartBpTesseractNoLanguageSelected"); return; }
            TesseractLanguages = string.Join('+', languages);
            await _tesseractDataAssetManager.InstallLanguagesAsync(languages);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AiLastError = ex.ToString(); }
    }

    /// <summary>
    /// 取消 Tesseract traineddata 下载。
    /// </summary>
    [RelayCommand] private void CancelTesseractDataDownload() => _tesseractDataAssetManager.Cancel();

    /// <summary>
    /// 刷新 Tesseract 语言数据安装状态并更新 UI 提示。
    /// </summary>
    /// <returns>刷新任务。</returns>
    [RelayCommand]
    private async Task RefreshTesseractDataStatusForUiAsync()
    {
        try
        {
            await RefreshTesseractDataStatusAsync();
            TesseractDownloadDetail = ResolveLocalizedOrRaw("SmartBpRefreshStatus");
        }
        catch (Exception ex)
        {
            AiLastError = ex.ToString();
            TesseractDownloadDetail = ex.ToString();
        }
    }

    /// <summary>
    /// 删除当前勾选的 Tesseract traineddata 语言文件。
    /// </summary>
    /// <returns>删除任务。</returns>
    [RelayCommand]
    private async Task DeleteTesseractDataAsync()
    {
        try
        {
            var languages = GetSelectedTesseractLanguages();
            if (languages.Length == 0) { AiLastError = ResolveLocalizedOrRaw("SmartBpTesseractNoLanguageSelected"); return; }
            await _tesseractDataAssetManager.DeleteAsync(languages);
            await RefreshTesseractDataStatusAsync();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    /// <summary>
    /// 从资产管理器同步 Tesseract 语言安装状态。
    /// </summary>
    /// <returns>刷新任务。</returns>
    private async Task RefreshTesseractDataStatusAsync()
    {
        var status = await _tesseractDataAssetManager.GetStatusAsync();
        foreach (var option in TesseractLanguageOptions)
            option.IsInstalled = status.InstalledLanguages.Contains(option.Language, StringComparer.OrdinalIgnoreCase);
        RefreshOcrProviderStatuses();
    }

    /// <summary>
    /// 初始化 RapidOCR 配置档列表并刷新当前模型安装状态。
    /// </summary>
    /// <returns>初始化任务。</returns>
    private async Task InitializeRapidOcrAsync()
    {
        try
        {
            RapidOcrModelProfiles = await _rapidOcrModelAssetManager.GetAvailableProfilesAsync();
            SelectedRapidOcrModelProfile = RapidOcrModelProfiles.FirstOrDefault(profile =>
                profile.Id == _recognitionSettingsService.Settings.SelectedRapidOcrModelId)
                ?? RapidOcrModelProfiles.FirstOrDefault();
            await RefreshRapidOcrStatusAsync();
        }
        catch
        {
            RapidOcrStatus = ResolveLocalizedOrRaw("SmartBpOcrStatusMissing");
            RapidOcrDownloadDetail = "";
        }
    }

    /// <summary>
    /// 下载或更新当前选择的 RapidOCR 模型配置档。
    /// </summary>
    /// <returns>下载任务。</returns>
    [RelayCommand(CanExecute = nameof(CanDownloadRapidOcrModel))]
    private async Task DownloadRapidOcrModelAsync()
    {
        if (SelectedRapidOcrModelProfile == null) return;
        try
        {
            await _rapidOcrModelAssetManager.InstallAsync(SelectedRapidOcrModelProfile.Id);
            await RefreshRapidOcrStatusAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    /// <summary>
    /// 取消 RapidOCR 模型下载。
    /// </summary>
    [RelayCommand]
    private void CancelRapidOcrDownload() => _rapidOcrModelAssetManager.Cancel();

    /// <summary>
    /// 删除当前选择的 RapidOCR 模型配置档。
    /// </summary>
    /// <returns>删除任务。</returns>
    [RelayCommand(CanExecute = nameof(CanDeleteRapidOcrModel))]
    private async Task DeleteRapidOcrModelAsync()
    {
        if (SelectedRapidOcrModelProfile == null) return;
        try
        {
            await _rapidOcrModelAssetManager.DeleteAsync(SelectedRapidOcrModelProfile.Id);
            await RefreshRapidOcrStatusAsync();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    /// <summary>
    /// 判断是否允许下载或更新当前 RapidOCR 模型。
    /// </summary>
    private bool CanDownloadRapidOcrModel() =>
        SelectedRapidOcrModelProfile != null &&
        !IsRapidOcrDownloading &&
        (!IsSelectedRapidOcrModelInstalled || IsRapidOcrUpdateAvailable);

    /// <summary>
    /// 判断是否允许删除当前 RapidOCR 模型。
    /// </summary>
    private bool CanDeleteRapidOcrModel() =>
        SelectedRapidOcrModelProfile != null &&
        !IsRapidOcrDownloading &&
        IsSelectedRapidOcrModelInstalled;

    /// <summary>
    /// 刷新 RapidOCR 当前配置档的安装、版本和更新状态。
    /// </summary>
    /// <returns>刷新任务。</returns>
    [RelayCommand]
    private async Task RefreshRapidOcrStatusAsync()
    {
        try
        {
            var status = await _rapidOcrModelAssetManager.GetStatusAsync();
            RapidOcrModelDirectory = status.ModelDirectory;
            RapidOcrInstalledVersion = status.InstalledVersion ?? ResolveLocalizedOrRaw("SmartBpRapidOcrVersionUnknown");
            RapidOcrLatestVersion = status.LatestVersion ?? "-";
            IsSelectedRapidOcrModelInstalled = status.IsInstalled;
            IsRapidOcrUpdateAvailable = status.HasUpdate;
            RapidOcrInstallActionText = ResolveLocalizedOrRaw(status.HasUpdate ? "SmartBpRapidOcrUpdate" : "Download");
            RapidOcrStatus = !status.IsInstalled
                ? ResolveLocalizedOrRaw("SmartBpOcrStatusMissing")
                : status.HasUpdate
                    ? string.Format(ResolveLocalizedOrRaw("SmartBpRapidOcrUpdateAvailableFormat"), RapidOcrInstalledVersion, RapidOcrLatestVersion)
                    : string.Format(ResolveLocalizedOrRaw("SmartBpRapidOcrUpToDateFormat"), RapidOcrLatestVersion);
            if (!IsRapidOcrDownloading) RapidOcrDownloadDetail = "";
        }
        catch
        {
            RapidOcrStatus = ResolveLocalizedOrRaw("SmartBpOcrStatusMissing");
            RapidOcrDownloadDetail = "";
            IsSelectedRapidOcrModelInstalled = false;
            IsRapidOcrUpdateAvailable = false;
        }
        finally
        {
            DownloadRapidOcrModelCommand.NotifyCanExecuteChanged();
            DeleteRapidOcrModelCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 通过远程清单检查 RapidOCR 官方模型是否有可安装更新。
    /// </summary>
    /// <returns>检查任务。</returns>
    [RelayCommand]
    private async Task CheckRapidOcrModelUpdateAsync()
    {
        if (SelectedRapidOcrModelProfile == null) return;
        try
        {
            await RefreshRapidOcrStatusAsync();
            var result = await _rapidOcrModelAssetManager.CheckForUpdatesAsync(SelectedRapidOcrModelProfile.Id);
            RapidOcrLatestVersion = result.OfficialVersion;
            if (!result.IsBundledManifestCurrent)
            {
                IsRapidOcrUpdateAvailable = false;
                DownloadRapidOcrModelCommand.NotifyCanExecuteChanged();
                RapidOcrStatus = string.Format(
                    ResolveLocalizedOrRaw("SmartBpRapidOcrBundledManifestOutdatedFormat"),
                    result.BundledVersion,
                    result.OfficialVersion);
                return;
            }

            if (result.HasInstallableUpdate)
            {
                IsRapidOcrUpdateAvailable = true;
                RapidOcrInstallActionText = ResolveLocalizedOrRaw("SmartBpRapidOcrUpdate");
                RapidOcrStatus = string.Format(
                    ResolveLocalizedOrRaw("SmartBpRapidOcrUpdateAvailableFormat"),
                    result.InstalledVersion ?? ResolveLocalizedOrRaw("SmartBpRapidOcrVersionUnknown"),
                    result.OfficialVersion);
            }
            else if (result.InstalledVersion != null)
            {
                IsRapidOcrUpdateAvailable = false;
                RapidOcrStatus = string.Format(
                    ResolveLocalizedOrRaw("SmartBpRapidOcrUpToDateFormat"),
                    result.OfficialVersion);
            }
        }
        catch (Exception ex)
        {
            RapidOcrDownloadDetail = ex.Message;
            AiLastError = ex.Message;
        }
        finally
        {
            DownloadRapidOcrModelCommand.NotifyCanExecuteChanged();
            DeleteRapidOcrModelCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void OpenRapidOcrModelFolder()
    {
        if (string.IsNullOrWhiteSpace(RapidOcrModelDirectory)) return;
        Directory.CreateDirectory(RapidOcrModelDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", RapidOcrModelDirectory) { UseShellExecute = true });
    }
    private async Task RecognizeCurrentFrameCoreAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true);
        if (frame == null) return;
        await RunFullStrategyRecognitionCoreAsync(frame);
    }

    private async Task DetectStageCoreAsync(BitmapSource frame)
        => await RunPhaseOnlyRecognitionCoreAsync(frame);

    private async Task RunAutomaticCurrentFrameCoreAsync()
    {
        if (!_windowCaptureService.IsCapturing)
        {
            await StopAutomaticRecognitionForCaptureIssueAsync("SmartBpRecognitionPausedCaptureStopped");
            return;
        }

        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
        {
            _automaticRecognitionUnavailableFrameCount++;
            if (_automaticRecognitionUnavailableFrameCount >= 1)
                await StopAutomaticRecognitionForCaptureIssueAsync("SmartBpRecognitionPausedFrameUnavailable");
            return;
        }

        _automaticRecognitionUnavailableFrameCount = 0;
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            var result = await _autoRecognitionCoordinator.RunOneTickAsync(frame);
            ApplyRegionGatedResult(result);
            if (result.SceneGate?.ShouldPauseAutomaticRecognition == true && IsAiPreviewLoopRunning)
            {
                await RequestStopAutomaticRecognitionAfterQueueDrainedAsync(result);
            }
        }
        catch (OperationCanceledException) { }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task StopAutomaticRecognitionForCaptureIssueAsync(string messageKey)
    {
        var message = ResolveLocalizedOrRaw(messageKey);
        _aiPreviewTimer.Stop();
        await _autoRecognitionCoordinator.StopAsync();
        IsAiPreviewLoopRunning = false;
        IsAiRecognizing = false;
        _autoRecognitionGlobalControl.Update(false);
        _automaticRecognitionUnavailableFrameCount = 0;
        AiLastError = message;
        AiSceneDiagnostics = string.IsNullOrWhiteSpace(AiSceneDiagnostics)
            ? message
            : AiSceneDiagnostics + Environment.NewLine + message;
        _aiDebugLog.Write("recognition", message);
        NotifyAutomaticRecognitionCommands();
        await MessageBoxHelper.ShowInfoAsync(message);
    }

    private async Task RequestStopAutomaticRecognitionAfterQueueDrainedAsync(SmartBpAutoRecognitionTickResult result)
    {
        if (_isAutomaticRecognitionStopPendingAfterQueueDrain)
            return;

        _isAutomaticRecognitionStopPendingAfterQueueDrain = true;
        _aiPreviewTimer.Stop();
        var phase = result.PhaseResult?.Phase ?? result.BusinessState?.Phase ?? "未知";
        var scene = result.SceneGate?.Scene.ToString() ?? "Unknown";
        var pendingMessage =
            $"Post-BP phase detected: phase={phase}; scene={scene}.{Environment.NewLine}" +
            "Character BP has ended; no new recognition ticks will be scheduled." + Environment.NewLine +
            "Automatic recognition stop is queued after pending operations drain." + Environment.NewLine +
            "Skipped content field recognition because BP ended.";
        AiSceneDiagnostics = string.IsNullOrWhiteSpace(AiSceneDiagnostics)
            ? pendingMessage
            : AiSceneDiagnostics + Environment.NewLine + pendingMessage;
        _aiDebugLog.Write("recognition", pendingMessage);

        await CompleteAutomaticRecognitionAfterQueueDrainedAsync("SmartBpCharacterBpEnded");
    }

    private async Task CompleteAutomaticRecognitionAfterQueueDrainedAsync(string reason)
    {
        await _autoRecognitionCoordinator.CompleteAsync();
        _gameGuidanceService.CompleteGuidance(reason);
        IsAiPreviewLoopRunning = false;
        _autoRecognitionGlobalControl.Update(false);
        _isAutomaticRecognitionStopPendingAfterQueueDrain = false;
        var completedMessage =
            $"Pending BP operation queue drained.{Environment.NewLine}" +
            $"Automatic recognition stopped.{Environment.NewLine}" +
            $"GameGuidance completed with reason={reason}.";
        AiSceneDiagnostics = string.IsNullOrWhiteSpace(AiSceneDiagnostics)
            ? completedMessage
            : AiSceneDiagnostics + Environment.NewLine + completedMessage;
        _aiDebugLog.Write("recognition", completedMessage);
        AiLastError = ResolveLocalizedOrRaw("SmartBpRecognitionPausedBpEnded");
        NotifyAutomaticRecognitionCommands();
    }

    private async Task RunRegionGatedFrameCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            var result = await _autoRecognitionCoordinator.RunOneTickAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunFullStrategyRecognitionCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            DebugModeSummary = "FullImage";
            var result = await _autoRecognitionCoordinator.RunFullRecognitionDebugAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunPureAiFullRecognitionDebugCoreAsync(BitmapSource frame)
        => await RunFullStrategyRecognitionCoreAsync(frame);

    private async Task RunPhaseOnlyRecognitionCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            DebugModeSummary = "PhaseOnly";
            var result = await _autoRecognitionCoordinator.RunPhaseOnlyDebugAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunIncrementalRecognitionCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            DebugModeSummary = "CurrentStageIncremental";
            var result = await _autoRecognitionCoordinator.RunIncrementalRecognitionDebugAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunOcrSelectedTestFrameCoreAsync(BitmapSource frame, SmartBpRecognitionTask task)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            var watch = Stopwatch.StartNew();
            var regions = GetOcrContentRegionsForTestFrame(task);
            var result = await _ocrBpRecognitionService.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(regions));
            watch.Stop();

            AiRawResponse = FormatOcrRawLines(result);
            AiStageDetectionResult = FormatBusinessState(result.BusinessState);
            AiGuidanceSnapshot = FormatGuidance(_gameGuidanceService.GetRuntimeSnapshot(), "OCR selected test frame uses direct OCR regions.");
            AiCandidateOperations = string.Join(Environment.NewLine, result.Diagnostics);
            AiParsedVisualResult = AiStageDetectionResult;
            AiNormalizedResult = AiCandidateOperations;
            AiPhaseCropPreview = null;
            AiFocusedCropPreview = null;
            AiCropDebugInfo = $"OCR selected test frame task={task}; regions=[{string.Join(", ", regions.Select(GetRecognitionRegionId))}]";
            AiSceneDiagnostics = "OCR selected test frame bypasses automatic scene gating.";
            AiRequestMetrics = $"OCR request elapsed: {watch.ElapsedMilliseconds}ms; regions=[{string.Join(", ", regions.Select(GetRecognitionRegionId))}]";
            AiLastError = "";
        }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private void ApplyRegionGatedResult(SmartBpAutoRecognitionTickResult result)
    {
        RefreshStrategyDebugSections(result);
        AiRawResponse = result.RawJson;
        AiStageDetectionResult = result.BusinessState == null ? "-" : FormatBusinessState(result.BusinessState);
        AiGuidanceSnapshot = FormatGuidance(result.GuidanceSnapshot, result.GuidanceSync?.Reason);
        AiCandidateOperations = FormatAutomaticOperations(result);
        LastSmartBpProgressDiagnosis = FormatProgressDiagnosis(result);
        AiParsedVisualResult = AiStageDetectionResult;
        AiNormalizedResult = AiCandidateOperations;
        AiPhaseCropPreview = result.PhaseCrop?.Image;
        AiFocusedCropPreview = result.ContentCrops?.LastOrDefault()?.Image ?? result.FocusedCrop?.Image;
        AiCropDebugInfo = FormatCropDebugInfo(result);
        AiSceneDiagnostics = result.SceneGate == null ? "-" :
            $"Scene: {result.SceneGate.Scene}{Environment.NewLine}BP recognition allowed: {result.SceneGate.IsBpRecognitionAllowed}{Environment.NewLine}Character operations allowed: {result.SceneGate.IsCharacterOperationAllowed}{Environment.NewLine}Action: {(result.SceneGate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}{Environment.NewLine}Reason: {result.SceneGate.Reason}";
        AiRequestMetrics = FormatRecognitionTiming();
        AiLastError = result.Error ?? "";
        RefreshRecognitionDebugLogText();
    }

    private string FormatProgressSyncResult(SmartBpProgressSyncResult result)
    {
        if (!result.Succeeded) return LocalizeProgressSyncMessage(result.Message) ?? FormatLocalizedDetail("SmartBpProgressForceSyncFailedFormat", result.Message);
        if (!result.Moved) return ResolveLocalizedOrRaw("SmartBpProgressDiagnosisAligned");
        return string.Format(
            ResolveLocalizedOrRaw("SmartBpProgressDiagnosisSyncedFormat"),
            result.TargetStepIndex,
            result.TargetAction,
            string.Join(",", result.TargetIndexes));
    }

    private void ShowForceSyncProgressInfoBar(SmartBpProgressSyncResult result)
    {
        var message = FormatProgressSyncResult(result);
        if (!result.Succeeded)
        {
            _infoBarService.ShowWarningInfoBar(message);
            return;
        }

        if (result.Moved)
            _infoBarService.ShowSuccessInfoBar(message);
        else
            _infoBarService.ShowInformationalInfoBar(message);
    }

    private string FormatProgressDiagnosis(SmartBpAutoRecognitionTickResult result)
    {
        if (result.ProgressSync?.Moved == true)
            return string.Format(
                ResolveLocalizedOrRaw("SmartBpProgressDiagnosisSyncedFormat"),
                result.ProgressSync.TargetStepIndex,
                result.ProgressSync.TargetAction,
                string.Join(",", result.ProgressSync.TargetIndexes));
        if (result.ProgressAlignment?.IsAligned == true) return ResolveLocalizedOrRaw("SmartBpProgressDiagnosisAligned");
        if (result.ProgressAlignment?.IsMisaligned == true)
            return string.Format(
                ResolveLocalizedOrRaw("SmartBpProgressDiagnosisMisalignedFormat"),
                result.GuidanceSnapshot.CurrentStepIndex,
                result.GuidanceSnapshot.CurrentAction,
                string.Join(",", result.GuidanceSnapshot.CurrentIndexes),
                result.ProgressAlignment.Inference.TargetStepIndex,
                result.ProgressAlignment.Inference.TargetAction,
                string.Join(",", result.ProgressAlignment.Inference.TargetIndexes));
        if (result.ProgressAlignment?.IsAmbiguous == true) return ResolveLocalizedOrRaw("SmartBpProgressDiagnosisInsufficient");
        return "-";
    }

    private string? LocalizeProgressSyncMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        return message switch
        {
            "An automatic recognition tick is already running." => ResolveLocalizedOrRaw("SmartBpProgressForceSyncRecognitionBusy"),
            "Inference ambiguous; no automatic movement." => ResolveLocalizedOrRaw("SmartBpProgressDiagnosisInsufficient"),
            "GameGuidance could not be started for progress sync." => ResolveLocalizedOrRaw("SmartBpProgressForceSyncGuidanceStartFailed"),
            "Automatic progress sync only moves forward." => ResolveLocalizedOrRaw("SmartBpProgressForceSyncForwardOnly"),
            "Current GameGuidance step already matches inferred progress." => ResolveLocalizedOrRaw("SmartBpProgressDiagnosisAligned"),
            "GameGuidance is not started or workflow is empty." => ResolveLocalizedOrRaw("SmartBpProgressForceSyncGuidanceStartFailed"),
            _ => null
        };
    }

    private string FormatLocalizedDetail(string formatKey, string? detail) =>
        string.Format(ResolveLocalizedOrRaw(formatKey), string.IsNullOrWhiteSpace(detail)
            ? ResolveLocalizedOrRaw("UnknownError")
            : detail);

    private void ResetStrategyDebugSections()
    {
        DebugStrategySummary = "-";
        DebugFinalBusinessState = "Recognition failed before final business state was produced.";
        DebugFusionSummary = "-";
        DebugPhaseScene = "-";
        DebugBusinessAiRaw = "-";
        DebugPureAiFullRaw = "-";
        DebugOcrRawLines = "-";
        DebugAiOcrTranscript = "-";
        DebugParsedState = "-";
        DebugMergeLog = "-";
        DebugCandidateOperations = "-";
        DebugServerStatus = FormatRoleServerStatus();
        DebugTiming = "-";
        RefreshRecognitionDebugLogText();
    }

    private void RefreshStrategyDebugSections(SmartBpAutoRecognitionTickResult result)
    {
        var strategy = _recognitionSettingsService.Settings.RecognitionStrategy;
        RefreshRoleServerStatus();
        DebugStrategySummary = $"debug_mode={DebugModeSummary}{Environment.NewLine}{FormatStrategySummary(strategy)}";
        DebugPhaseScene = result.SceneGate == null
            ? $"phase={result.PhaseResult?.Phase ?? "unknown"}"
            : $"phase={result.PhaseResult?.Phase ?? "unknown"}{Environment.NewLine}scene={result.SceneGate.Scene}{Environment.NewLine}bp_allowed={result.SceneGate.IsBpRecognitionAllowed}{Environment.NewLine}character_operations_allowed={result.SceneGate.IsCharacterOperationAllowed}{Environment.NewLine}reason={result.SceneGate.Reason}";
        DebugParsedState = result.BusinessState == null ? "-" : FormatBusinessState(result.BusinessState);
        DebugFinalBusinessState = result.BusinessState == null
                ? "Recognition failed before final business state was produced."
                : DebugParsedState;
        DebugFusionSummary = "No hybrid fusion was used.";
        DebugCandidateOperations = FormatAutomaticOperations(result);
        DebugTiming = FormatRecognitionTiming();
        DebugServerStatus = FormatRoleServerStatus();
        DebugMergeLog = string.Join(Environment.NewLine, result.CandidateMessages.Where(message =>
            message.Contains("Applied ", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("merge", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("parsed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("rejected", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("overridden", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("request failed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("business_ai_model=", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("ai_ocr_model=", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("structured_output_mode=", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("strict_schema_enabled=", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("updates=", StringComparison.OrdinalIgnoreCase)));
        if (string.IsNullOrWhiteSpace(DebugMergeLog)) DebugMergeLog = "-";

        DebugBusinessAiRaw = "-";
        DebugPureAiFullRaw = "-";
        DebugOcrRawLines = ExtractOcrRaw(result.RawJson);
        DebugAiOcrTranscript = "-";
        RefreshRecognitionDebugLogText();
    }

    [RelayCommand]
    private void OpenRecognitionDebugLogWindow()
    {
        RefreshRecognitionDebugLogText();
        if (_recognitionDebugLogWindow is { IsVisible: true })
        {
            _recognitionDebugLogWindow.Activate();
            return;
        }
        var window = new SmartBpRecognitionDebugLogWindow(this)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive)
                    ?? Application.Current?.MainWindow
        };
        window.Closed += (_, _) => _recognitionDebugLogWindow = null;
        _recognitionDebugLogWindow = window;
        window.Show();
    }

    [RelayCommand]
    private void CopyRecognitionDebugLog()
    {
        RefreshRecognitionDebugLogText();
        if (!string.IsNullOrWhiteSpace(RecognitionDebugLogText))
            Clipboard.SetText(RecognitionDebugLogText);
    }

    [RelayCommand]
    private void RefreshRecognitionDebugLog() => RefreshRecognitionDebugLogText();

    private void RefreshRecognitionDebugLogText()
    {
        RecognitionDebugLogText = $"""
=== Final Business State ===
{DebugFinalBusinessState}

=== Debug Mode / Strategy ===
{DebugStrategySummary}

=== Phase / Scene ===
{DebugPhaseScene}

=== OCR Raw Lines ===
{DebugOcrRawLines}

=== Merge / Validation Diagnostics ===
{DebugMergeLog}

=== Candidate Operations ===
{DebugCandidateOperations}

=== Timing ===
{DebugTiming}

=== Runtime Log ===
{AiDebugLogText}

=== Last Error ===
{AiLastError}
""";
    }

    partial void OnAiDebugLogTextChanged(string value) => RefreshRecognitionDebugLogText();

    private string FormatStrategySummary(SmartBpRecognitionStrategy strategy) =>
        strategy switch
        {
            SmartBpRecognitionStrategy.PureOcr =>
                $"strategy=PureOcr{Environment.NewLine}ocr_provider={_recognitionSettingsService.Settings.SelectedOcrProviderMode}",
            _ => $"strategy={strategy}"
        };

    private string FormatRecognitionTiming()
        => $"OCR interval: {CurrentRecognitionIntervalMs}ms; minimum measured interval: {MinimumRecognitionIntervalMs}ms.";

    private static string ExtractRawSection(string raw, string key)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "-";
        var marker = $"{key} raw:";
        var index = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            marker = $"{key} rejected raw:";
            index = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        }
        if (index < 0 && key == "phase_only" && raw.TrimStart().StartsWith('{')) return raw;
        if (index < 0) return "-";
        var start = index + marker.Length;
        var next = raw.IndexOf("\n\n", start, StringComparison.Ordinal);
        return (next < 0 ? raw[start..] : raw[start..next]).Trim();
    }

    private static string ExtractOcrRaw(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "-";
        var index = raw.IndexOf("ocr raw:", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? "-" : raw[(index + "ocr raw:".Length)..].Trim();
    }

    private static string ExtractAiOcrRaw(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "-";
        var lines = raw.Split(["\n\n"], StringSplitOptions.None)
            .Where(section => section.Contains("ai_ocr_", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return lines.Length == 0 ? "-" : string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private BitmapSource LoadTestFrame(SmartBpTestFrame frame)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(Path.Combine(_smartBpModuleStorage.ModuleRoot, "Resources", "SmartBp", "TestFrames", frame.FileName));
        image.EndInit(); image.Freeze(); return image;
    }

    private static string FormatStage(SmartBpStageDetectionResult value) =>
        $"action={value.RecognizedAction}; activeSide={value.ActiveSide}; region={value.OperationRegion}; owner={value.OperationOwner}; targetCamp={value.TargetCamp}; confidence={value.Confidence:0.00}{Environment.NewLine}" +
        $"leftTopTitle={value.LeftTopTitle ?? "null"}; rightTopTitle={value.RightTopTitle ?? "null"}; status={value.MainStatus ?? "null"}{Environment.NewLine}" +
        $"evidence={string.Join(" | ", value.Evidence)}{Environment.NewLine}warnings={string.Join(" | ", value.Warnings)}";

    private string FormatBusinessState(SmartBpBusinessStateRecognitionResult value) =>
        SmartBpBusinessStateFormatter.Format(value, _smartBpCharacterResolver, includeResolved: true);

    private static IReadOnlyList<SmartBpRecognitionRegion> GetOcrContentRegionsForTestFrame(SmartBpRecognitionTask task) =>
        task switch
        {
            SmartBpRecognitionTask.BanSur => [SmartBpRecognitionRegion.RightTop],
            SmartBpRecognitionTask.BanHun => [SmartBpRecognitionRegion.LeftTop],
            SmartBpRecognitionTask.PickSur or SmartBpRecognitionTask.CharacterDistribution => [SmartBpRecognitionRegion.LeftBottom],
            SmartBpRecognitionTask.PickHun => [SmartBpRecognitionRegion.RightBottom],
            _ => [SmartBpRecognitionRegion.RightTop, SmartBpRecognitionRegion.LeftTop, SmartBpRecognitionRegion.LeftBottom, SmartBpRecognitionRegion.RightBottom]
        };

    private static string FormatOcrRawLines(SmartBpOcrRecognitionResult result)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"phase={result.Phase.Phase}");
        foreach (var region in result.Regions)
        {
            builder.AppendLine($"[{GetRecognitionRegionId(region.Region)}]");
            foreach (var line in region.Lines)
                builder.AppendLine($"provider={line.Provider ?? "unknown"}\tcoordinateSpace=region-local\ttext={line.Text}\tbbox={line.BoundingBox}\tcenter={line.CenterX:0.0},{line.CenterY:0.0}\tconf={line.Confidence:0.00}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetRecognitionRegionId(SmartBpRecognitionRegion region) =>
        region switch
        {
            SmartBpRecognitionRegion.PhaseTop => "phase_top",
            SmartBpRecognitionRegion.TopCenterStatus => "top_center_status",
            SmartBpRecognitionRegion.TopLeftStatus => "top_left_status",
            SmartBpRecognitionRegion.LeftTop => "left_top",
            SmartBpRecognitionRegion.RightTop => "right_top",
            SmartBpRecognitionRegion.LeftBottom => "left_bottom",
            SmartBpRecognitionRegion.RightBottom => "right_bottom",
            _ => region.ToString()
        };

    private static string FormatGuidance(Core.Models.GameGuidanceRuntimeSnapshot value, string? reason = null) =>
        $"started={value.IsStarted}; step={value.CurrentStepIndex}; action={value.CurrentAction?.ToString() ?? "null"}; indexes=[{string.Join(", ", value.CurrentIndexes)}]; time={value.CurrentTime?.ToString() ?? "null"}{Environment.NewLine}{reason ?? ""}";

    private static string FormatOperation(SmartBpDetectedOperation value) =>
        $"{value.Kind}: step={value.SourceWorkflowStepIndex?.ToString() ?? "none"}; mode={value.ApplyMode}; action={value.SourceGuidanceAction}; indexes=[{string.Join(", ", value.SourceGuidanceIndexes)}]; camp={value.Camp}; slot={value.SlotIndex}; raw={value.RawCharacterName ?? "null"}; resolved={value.ResolvedCharacterName ?? "unresolved"}; playerId={value.PlayerId ?? "null"}; confidence={value.Confidence:0.00}; {value.Reason}";

    private static string FormatAutomaticOperations(SmartBpAutoRecognitionTickResult result)
    {
        var builder = new System.Text.StringBuilder();
        if (result.BackfillPlan != null)
        {
            builder.AppendLine("Workflow backfill plan:");
            foreach (var step in result.BackfillPlan.StepCandidates)
            {
                builder.AppendLine($"Step {step.StepIndex} {step.Action} [{string.Join(", ", step.Indexes)}] - {step.Reason}");
                if (step.Operations.Count == 0) builder.AppendLine("  no character operation");
                foreach (var operation in step.Operations) builder.AppendLine("  " + FormatOperation(operation));
            }
            builder.AppendLine();
        }
        builder.AppendLine("Candidate operations:");
        if (result.Operations.Count == 0)
            builder.AppendLine("-");
        else
            foreach (var operation in result.Operations) builder.AppendLine(FormatOperation(operation));
        if (result.CandidateMessages.Count > 0)
        {
            builder.AppendLine().AppendLine("Candidate diagnostics:");
            foreach (var message in result.CandidateMessages) builder.AppendLine(message);
        }
        if (result.ApplyResult != null)
        {
            builder.AppendLine().AppendLine($"Apply result: applied={result.ApplyResult.AppliedCount}; skipped={result.ApplyResult.SkippedCount}");
            foreach (var message in result.ApplyResult.Messages) builder.AppendLine(message);
        }
        return builder.ToString().TrimEnd();
    }

    private static string FormatCropDebugInfo(SmartBpAutoRecognitionTickResult result)
    {
        var builder = new System.Text.StringBuilder();
        if (result.PhaseCrop != null) builder.AppendLine($"Phase crop: {result.PhaseCrop.Region}, pixel rect = {result.PhaseCrop.PixelRectText}");
        if (result.PhaseResult != null) builder.AppendLine($"Detected phase: {result.PhaseResult.Phase}");
        if (result.FocusedCrop != null) builder.AppendLine($"Focused crop: {result.FocusedCrop.Region}, pixel rect = {result.FocusedCrop.PixelRectText}");
        if (result.ContentCrops != null)
            foreach (var crop in result.ContentCrops)
                builder.AppendLine($"Snapshot crop: {crop.Region}, pixel rect = {crop.PixelRectText}");
        if (result.FocusedResult != null)
        {
            builder.AppendLine($"Focused target field: {result.FocusedResult.TargetField}");
            if (result.FocusedResult.TargetField == "picked_hun" && result.FocusedResult.PickedHun != null)
                builder.AppendLine($"[0] {result.FocusedResult.PickedHun.CharacterName} / {result.FocusedResult.PickedHun.PlayerId ?? "null"}");
            foreach (var slot in result.FocusedResult.Slots)
                builder.AppendLine($"[{slot.Index}] {slot.CharacterName} / {slot.PlayerId ?? "null"}");
        }
        return builder.Length == 0 ? "-" : builder.ToString().TrimEnd();
    }

    private static string FormatFocused(SmartBpFocusedExtractionResult value) =>
        $"task={value.Task}; region={value.OperationRegion}; targetCamp={value.TargetCamp}{Environment.NewLine}" +
        string.Join(Environment.NewLine, value.Slots.Select(x => $"slot[{x.SlotIndex}] state={x.SlotState} character={x.CharacterName ?? "null"} playerId={x.PlayerId ?? "null"} banned={x.IsBannedOrUnavailable} confidence={x.Confidence:0.00} raw={x.RawVisibleText ?? "null"}"));

    private void RefreshGuidanceSnapshot() => AiGuidanceSnapshot = FormatGuidance(_gameGuidanceService.GetRuntimeSnapshot());
    private async Task RecognizeCoreAsync(BitmapSource frame, SmartBpRecognitionTask task)
        => await RunOcrSelectedTestFrameCoreAsync(frame, task);

    [ObservableProperty]
    public partial string AiParsedVisualResult { get; set; } = "";

    partial void OnSelectedAiPromptProfileChanged(SmartBpPromptProfile? value)
    {
        if (value == null || value.Id == _recognitionSettingsService.Settings.PromptProfileId) return;
        _recognitionSettingsService.Settings.PromptProfileId = value.Id;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnSelectedQwenModelProfileChanged(QwenModelProfile? value)
    {
        if (_isSwitchingQwenModel) return;
        _ = RefreshSelectedQwenModelInstallStatusAsync();
        SwitchSelectedQwenModelCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAiOcrModelProfileChanged(QwenModelProfile? value)
    {
        if (_isSwitchingAiOcrModel) return;
        _ = RefreshSelectedAiOcrModelInstallStatusAsync();
        SwitchSelectedAiOcrModelCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshSelectedQwenModelInstallStatusAsync()
    {
        try
        {
            IsSelectedQwenModelInstalled = SelectedQwenModelProfile != null &&
                await _qwenAssetManager.IsInstalledAsync(SelectedQwenModelProfile.Id);
        }
        catch
        {
            IsSelectedQwenModelInstalled = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSelectedAiOcrModelInstallStatusAsync()
    {
        try
        {
            IsSelectedAiOcrModelInstalled = SelectedAiOcrModelProfile != null &&
                await _qwenAssetManager.IsInstalledAsync(SelectedAiOcrModelProfile.Id);
            if (SelectedAiOcrModelProfile == null)
            {
                CurrentAiOcrModelDisplayName = "";
                AiOcrModelStatus = "-";
            }
            else
            {
                CurrentAiOcrModelDisplayName = SelectedAiOcrModelProfile.DisplayName;
                AiOcrModelStatus = IsSelectedAiOcrModelInstalled
                    ? ResolveLocalizedOrRaw("SmartBpAiStatusInstalled")
                    : ResolveLocalizedOrRaw("SmartBpAiStatusNotInstalled");
            }
        }
        catch (Exception ex)
        {
            IsSelectedAiOcrModelInstalled = false;
            AiOcrModelStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed");
            AiLastError = ex.Message;
        }
        finally
        {
            DownloadAiOcrModelCommand.NotifyCanExecuteChanged();
            DeleteAiOcrModelCommand.NotifyCanExecuteChanged();
            SwitchSelectedAiOcrModelCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSwitchSelectedQwenModel))]
    private async Task SwitchSelectedQwenModelAsync()
    {
        if (SelectedQwenModelProfile == null) return;
        await SwitchQwenModelAsync(SelectedQwenModelProfile);
    }

    private bool CanSwitchSelectedQwenModel() =>
        !IsBusinessAiModelDownloading && !IsAiOcrModelDownloading &&
        SelectedQwenModelProfile != null &&
        !string.Equals(SelectedQwenModelProfile.Id, _recognitionSettingsService.Settings.SelectedBusinessAiModelId, StringComparison.Ordinal);

    private async Task SwitchQwenModelAsync(QwenModelProfile value)
    {
        _isSwitchingQwenModel = true;
        var oldId = _recognitionSettingsService.Settings.SelectedBusinessAiModelId;
        var business = _llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi);
        var aiOcr = _llamaServerManagers.Get(LlamaVisionServerRole.AiOcr);
        var restartBusiness = business.IsRunning;
        var preserveAiOcrRole = false;
        var businessRestartStateSet = false;
        var aiOcrRestartStateSet = false;
        try
        {
            if (restartBusiness)
            {
                var confirmed = await MessageBoxHelper.ShowConfirmAsync(
                    ResolveLocalizedOrRaw("SmartBpAiSwitchModelRestartConfirm"),
                    ResolveLocalizedOrRaw("SmartBpAiSwitchModelTitle"),
                    ResolveLocalizedOrRaw("Confirm"), ResolveLocalizedOrRaw("Cancel"));
                if (!confirmed)
                {
                    SelectedQwenModelProfile = QwenModelProfiles.FirstOrDefault(profile => profile.Id == oldId);
                    return;
                }
                SetRoleServerStarting(LlamaVisionServerRole.BusinessAi, true, true);
                businessRestartStateSet = true;
                await business.StopAsync();
            }
            _recognitionSettingsService.Settings.SelectedBusinessAiModelId = value.Id;
            _recognitionSettingsService.Settings.SelectedQwenModelId = value.Id;
            RefreshRecognitionSpeedTestValidity();
            await SaveQwenSelectionAsync();
            CurrentQwenModelDisplayName = string.Format(ResolveLocalizedOrRaw("SmartBpCurrentQwenModelFormat"), value.DisplayName);
            IsQwenInstalled = await _qwenAssetManager.IsInstalledAsync(value.Id);
            await RefreshSelectedQwenModelInstallStatusAsync();
            SwitchSelectedQwenModelCommand.NotifyCanExecuteChanged();
            if (restartBusiness && IsQwenInstalled)
                await business.StartAsync();
            else if (!IsQwenInstalled)
                AiLastError = ResolveLocalizedOrRaw("SmartBpAiModelDownloadRequired");

            var aiOcrModelInstalled = await _qwenAssetManager.IsInstalledAsync(_recognitionSettingsService.Settings.SelectedAiOcrModelId);
            if (preserveAiOcrRole && !IsAiOcrReusingBusinessServer() && !aiOcr.IsRunning && aiOcrModelInstalled)
            {
                SetRoleServerStarting(LlamaVisionServerRole.AiOcr, true, true);
                aiOcrRestartStateSet = true;
                await aiOcr.StartAsync();
            }
            await ReconcileLlamaServersForCurrentStrategyAsync();
            RefreshLlamaServerUiState();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
        finally
        {
            if (businessRestartStateSet) SetRoleServerStarting(LlamaVisionServerRole.BusinessAi, false);
            if (aiOcrRestartStateSet) SetRoleServerStarting(LlamaVisionServerRole.AiOcr, false);
            _isSwitchingQwenModel = false;
            RefreshLlamaServerUiState();
        }
    }

    private async Task SwitchAiOcrModelAsync(QwenModelProfile value)
    {
        _isSwitchingAiOcrModel = true;
        var oldId = _recognitionSettingsService.Settings.SelectedAiOcrModelId;
        var business = _llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi);
        var aiOcr = _llamaServerManagers.Get(LlamaVisionServerRole.AiOcr);
        var wasReusingBusiness = IsAiOcrReusingBusinessServer();
        var aiOcrRoleWasRunning = wasReusingBusiness ? business.IsRunning : aiOcr.IsRunning;
        var aiOcrRestartStateSet = false;
        var businessRestartStateSet = false;
        try
        {
            if (aiOcrRoleWasRunning)
            {
                var confirmed = await MessageBoxHelper.ShowConfirmAsync(
                    ResolveLocalizedOrRaw("SmartBpAiSwitchModelRestartConfirm"),
                    ResolveLocalizedOrRaw("SmartBpAiSwitchModelTitle"),
                    ResolveLocalizedOrRaw("Confirm"), ResolveLocalizedOrRaw("Cancel"));
                if (!confirmed)
                {
                    SelectedAiOcrModelProfile = AiOcrModelProfiles.FirstOrDefault(profile => profile.Id == oldId);
                    return;
                }
            }

            if (!wasReusingBusiness && aiOcr.IsRunning)
            {
                SetRoleServerStarting(LlamaVisionServerRole.AiOcr, true, true);
                aiOcrRestartStateSet = true;
                await aiOcr.StopAsync();
            }

            _recognitionSettingsService.Settings.SelectedAiOcrModelId = value.Id;
            RefreshRecognitionSpeedTestValidity();
            await _recognitionSettingsService.SaveAsync();
            CurrentAiOcrModelDisplayName = value.DisplayName;
            await RefreshSelectedAiOcrModelInstallStatusAsync();

            if (aiOcrRoleWasRunning && IsAiOcrReusingBusinessServer() && !business.IsRunning &&
                     await _qwenAssetManager.IsInstalledAsync(_recognitionSettingsService.Settings.SelectedBusinessAiModelId))
            {
                SetRoleServerStarting(LlamaVisionServerRole.BusinessAi, true, true);
                businessRestartStateSet = true;
                await business.StartAsync();
            }
            else if (!IsAiOcrReusingBusinessServer() && !IsSelectedAiOcrModelInstalled)
                AiLastError = ResolveLocalizedOrRaw("SmartBpAiModelDownloadRequired");
            await ReconcileLlamaServersForCurrentStrategyAsync();
            RefreshLlamaServerUiState();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
        finally
        {
            if (aiOcrRestartStateSet) SetRoleServerStarting(LlamaVisionServerRole.AiOcr, false);
            if (businessRestartStateSet) SetRoleServerStarting(LlamaVisionServerRole.BusinessAi, false);
            _isSwitchingAiOcrModel = false;
            RefreshLlamaServerUiState();
        }
    }

    partial void OnSelectedLlamaRuntimeAssetChanged(LlamaCppRuntimeAssetSelection? value)
    {
        if (value == null || value.Id == _recognitionSettingsService.Settings.SelectedLlamaRuntimeId) return;
        if (_llamaServerManager.IsRunning)
        {
            // 服务运行时不允许切换运行时资产，回退到当前设置中的选择。
            var current = LlamaRuntimeAssets.FirstOrDefault(a => a.Id == _recognitionSettingsService.Settings.SelectedLlamaRuntimeId);
            if (current != null)
            {
                SelectedLlamaRuntimeAsset = current;
                return;
            }
        }
        _recognitionSettingsService.Settings.SelectedLlamaRuntimeId = value.Id;
        _recognitionSettingsService.Settings.LlamaServerExecutablePath = "";
        LlamaServerExecutablePath = "";
        RefreshRecognitionSpeedTestValidity();
        _ = SaveRuntimeSelectionAsync();
    }

    partial void OnEnableAutoGuidanceSyncChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableAutoGuidanceSync = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnEnableAutoApplyRecognitionChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableAutoApplyRecognition = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnEnableAutoGuidancePageNavigationChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableAutoGuidancePageNavigation = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnEnableSmartBpProgressAutoCorrectionChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableSmartBpProgressAutoCorrection = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionApplyModeChanged(SmartBpRecognitionApplyMode value)
    {
        _recognitionSettingsService.Settings.RecognitionApplyMode = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnIsAiRecognizingChanged(bool value)
    {
        StopCaptureCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAiPreviewLoopRunningChanged(bool value)
    {
        StopCaptureCommand.NotifyCanExecuteChanged();
    }

    partial void OnAiOneStepDelayedModeChanged(bool value)
    {
        _recognitionSettingsService.Settings.AiOneStepDelayedMode = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnAiUnknownPhaseTalentInferenceFramesChanged(int value)
    {
        _recognitionSettingsService.Settings.AiUnknownPhaseTalentInferenceFrames = Math.Clamp(value, 1, 30);
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnPlayBackfillAnimationsChanged(bool value)
    {
        _recognitionSettingsService.Settings.PlayBackfillAnimations = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnUseMultiImageSnapshotRequestChanged(bool value)
    {
        _recognitionSettingsService.Settings.UseMultiImageSnapshotRequest = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionBackfillLookBehindStepsChanged(int value)
    {
        _recognitionSettingsService.Settings.RecognitionBackfillLookBehindSteps = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionFieldStaleMillisecondsChanged(int value)
    {
        _recognitionSettingsService.Settings.RecognitionFieldStaleMilliseconds = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionVisualBufferMillisecondsChanged(int value)
    {
        _recognitionSettingsService.Settings.RecognitionVisualBufferMilliseconds = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnLlamaParallelSlotsChanged(int value)
    {
        _recognitionSettingsService.Settings.LlamaParallelSlots = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnLlamaGpuLayersChanged(int value)
    {
        _recognitionSettingsService.Settings.LlamaGpuLayers = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnLlamaFlashAttentionChanged(bool value)
    {
        _recognitionSettingsService.Settings.LlamaFlashAttention = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnLlamaBatchSizeChanged(int value)
    {
        _recognitionSettingsService.Settings.LlamaBatchSize = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnLlamaUBatchSizeChanged(int value)
    {
        _recognitionSettingsService.Settings.LlamaUBatchSize = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    [RelayCommand]
    private void ResetAiRecognitionLedger()
    {
        _aiRecognitionLedger.ResetForCurrentGame();
        _aiRecognitionStateStore.Reset();
        AiCandidateOperations = ResolveLocalizedOrRaw("SmartBpAiLedgerResetCompleted");
        _aiDebugLog.Write("Recognition", "Recognition ledger and local snapshot state reset for the current game.");
    }

    private async Task SaveRuntimeSelectionAsync()
    {
        await _recognitionSettingsService.SaveAsync();
        await RefreshLlamaRuntimeStatusAsync();
    }

    partial void OnSelectedRecognitionStrategyChanged(RecognitionStrategySelection? value)
    {
        if (value == null)
            return;

        _recognitionSettingsService.Settings.RecognitionStrategy = value.Strategy;
        _recognitionSettingsService.Settings.RecognitionEngine = value.Strategy == SmartBpRecognitionStrategy.PureOcr
            ? SmartBpRecognitionEngine.Ocr
            : SmartBpRecognitionEngine.AiQwen;
        RefreshRecognitionEngineVisibility();
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
        _ = ReconcileLlamaServersForCurrentStrategyAsync();
        _aiDebugLog.Write("Recognition", $"Recognition strategy switched to {value.Strategy}.");
    }

    partial void OnEnableOcrBpRecognitionChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableOcrBpRecognition = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnOcrRecognitionIntervalMsChanged(int value)
    {
        var minimum = Math.Max(100, _recognitionSettingsService.Settings.MinimumOcrRecognitionIntervalMs);
        _recognitionSettingsService.Settings.OcrRecognitionIntervalMs = Math.Clamp(value, minimum, 300000);
        RefreshRecognitionTimerInterval();
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionIntervalMsChanged(int value)
    {
        var minimum = Math.Max(100, _recognitionSettingsService.Settings.MinimumAiRecognitionIntervalMs);
        _recognitionSettingsService.Settings.RecognitionIntervalMs = Math.Clamp(value, minimum, 300000);
        RefreshRecognitionTimerInterval();
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnOcrFieldStaleMillisecondsChanged(int value)
    {
        _recognitionSettingsService.Settings.OcrFieldStaleMilliseconds = Math.Clamp(value, 250, 30000);
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnOcrBackfillLookBehindStepsChanged(int value)
    {
        _recognitionSettingsService.Settings.OcrBackfillLookBehindSteps = Math.Clamp(value, 0, 20);
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnUseOcrContactSheetChanged(bool value)
    {
        _recognitionSettingsService.Settings.UseOcrContactSheet = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnAiWithOcrFusionModeChanged(SmartBpHybridFusionMode value)
    {
        _recognitionSettingsService.Settings.AiWithOcrFusionMode = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnAiWithAiOcrFusionModeChanged(SmartBpHybridFusionMode value)
    {
        _recognitionSettingsService.Settings.AiWithAiOcrFusionMode = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnEnableOcrDebugOverlayChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableOcrDebugOverlay = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnSelectedOcrProviderChanged(OcrProviderSelection? value)
    {
        if (value == null) return;
        _recognitionSettingsService.Settings.SelectedOcrProviderMode = value.Mode;
        _recognitionSettingsService.Settings.OcrProviderMode = value.Mode;
        RefreshRecognitionEngineVisibility();
        RefreshRecognitionSpeedTestValidity();
        RefreshOcrProviderStatuses();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnSelectedRapidOcrModelProfileChanged(RapidOcrModelProfile? value)
    {
        DownloadRapidOcrModelCommand.NotifyCanExecuteChanged();
        DeleteRapidOcrModelCommand.NotifyCanExecuteChanged();
        if (value == null || value.Id == _recognitionSettingsService.Settings.SelectedRapidOcrModelId)
        {
            _ = RefreshRapidOcrStatusAsync();
            return;
        }
        _recognitionSettingsService.Settings.SelectedRapidOcrModelId = value.Id;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
        _ = RefreshRapidOcrStatusAsync();
    }

    partial void OnRapidOcrPaddingChanged(int value) => SaveRapidSettings(settings => settings.RapidOcrPadding = Math.Clamp(value, 0, 256));
    partial void OnRapidOcrMaxSideLenChanged(int value) => SaveRapidSettings(settings => settings.RapidOcrMaxSideLen = Math.Clamp(value, 320, 4096));
    partial void OnRapidOcrBoxScoreThresholdChanged(double value) => SaveRapidSettings(settings => settings.RapidOcrBoxScoreThreshold = Math.Clamp(value, 0, 1));
    partial void OnRapidOcrBoxThresholdChanged(double value) => SaveRapidSettings(settings => settings.RapidOcrBoxThreshold = Math.Clamp(value, 0, 1));
    partial void OnRapidOcrUnclipRatioChanged(double value) => SaveRapidSettings(settings => settings.RapidOcrUnclipRatio = Math.Clamp(value, .1, 5));
    partial void OnRapidOcrUseAngleClassifierChanged(bool value) => SaveRapidSettings(settings => settings.RapidOcrUseAngleClassifier = value);
    partial void OnRapidOcrUsePreprocessingVariantsChanged(bool value) => SaveRapidSettings(settings => settings.RapidOcrUsePreprocessingVariants = value);

    private void SaveRapidSettings(Action<SmartBpRecognitionSettings> update)
    {
        update(_recognitionSettingsService.Settings);
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnTesseractLanguagesChanged(string value)
    {
        _recognitionSettingsService.Settings.TesseractLanguages = string.IsNullOrWhiteSpace(value) ? "chi_sim+eng" : value.Trim();
        SyncSelectedTesseractLanguageOptions();
        RefreshRecognitionSpeedTestValidity();
        RefreshOcrProviderStatuses();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnEnableTesseractOcrChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableTesseractOcr = value;
        RefreshOcrProviderStatuses();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnTesseractDefaultPsmChanged(int value)
    {
        _recognitionSettingsService.Settings.TesseractDefaultPsm = Math.Clamp(value, 0, 13);
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnTesseractMaxPreprocessVariantsChanged(int value)
    {
        _recognitionSettingsService.Settings.TesseractMaxPreprocessVariants = Math.Clamp(value, 1, 3);
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    [RelayCommand]
    private void OpenTesseractDataFolder()
    {
        var status = _ocrService.GetProviderStatus(SmartBpOcrProviderKind.Tesseract);
        var path = status.DataPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void RefreshOcrProviderStatuses()
    {
        var paddle = _ocrService.GetProviderStatus(SmartBpOcrProviderKind.Paddle);
        var tesseract = _ocrService.GetProviderStatus(SmartBpOcrProviderKind.Tesseract);
        var rapid = _ocrService.GetProviderStatus(SmartBpOcrProviderKind.Rapid);
        PaddleOcrStatus = paddle.IsReady ? ResolveLocalizedOrRaw("SmartBpOcrStatusInstalled") : ResolveLocalizedOrRaw("SmartBpOcrStatusMissing");
        TesseractOcrStatus = tesseract.IsReady
            ? ResolveLocalizedOrRaw("SmartBpOcrStatusInstalled")
            : $"{ResolveLocalizedOrRaw("SmartBpOcrStatusMissing")}: {tesseract.Details}";
        RapidOcrStatus = rapid.IsReady
            ? ResolveLocalizedOrRaw("SmartBpOcrStatusInstalled")
            : ResolveLocalizedOrRaw("SmartBpOcrStatusMissing");
    }

    private string[] GetSelectedTesseractLanguages() =>
        TesseractLanguageOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Language)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void SyncSelectedTesseractLanguageOptions()
    {
        if (TesseractLanguageOptions.Count == 0)
            return;

        var selected = ParseTesseractLanguageExpression(TesseractLanguages).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var option in TesseractLanguageOptions)
            option.IsSelected = selected.Contains(option.Language);
    }

    private static IEnumerable<string> ParseTesseractLanguageExpression(string? languages) =>
        (string.IsNullOrWhiteSpace(languages) ? "chi_sim+eng" : languages)
        .Split(['+', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    partial void OnAllowSequentialSnapshotFallbackChanged(bool value)
    {
        _recognitionSettingsService.Settings.AllowSequentialSnapshotFallback = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnUseStrictCandidateEnumsInAutoSchemaChanged(bool value)
    {
        _recognitionSettingsService.Settings.UseStrictCandidateEnumsInAutoSchema = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnPhaseCropMaxImageWidthChanged(int value)
    {
        _recognitionSettingsService.Settings.PhaseCropMaxImageWidth = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnContentCropMaxImageWidthChanged(int value)
    {
        _recognitionSettingsService.Settings.ContentCropMaxImageWidth = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnPhaseMaxTokensChanged(int value)
    {
        _recognitionSettingsService.Settings.PhaseMaxTokens = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnSnapshotDeltaMaxTokensChanged(int value)
    {
        _recognitionSettingsService.Settings.SnapshotDeltaMaxTokens = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnPhaseTransitionCommitHoldMillisecondsChanged(int value)
    {
        _recognitionSettingsService.Settings.PhaseTransitionCommitHoldMilliseconds = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnPhaseTransitionCommitHoldMaxMillisecondsChanged(int value)
    {
        _recognitionSettingsService.Settings.PhaseTransitionCommitHoldMaxMilliseconds = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    private async Task SaveQwenSelectionAsync()
    {
        await _recognitionSettingsService.SaveAsync();
        await RefreshQwenStatusAsync();
    }

    private void ApplyVisionModelDownloadState(QwenDownloadState state)
    {
        var detail = FormatDownloadState(state);
        QwenDownloadStatus = ResolveLocalizedOrRaw(state.Status);
        if (_activeVisionModelDownloadRole == LocalVisionModelDownloadRole.AiOcr)
        {
            IsAiOcrModelDownloading = state.IsDownloading;
            AiOcrModelDownloadProgress = state.Progress ?? 0;
            AiOcrModelDownloadDetail = detail;
            IsBusinessAiModelDownloading = false;
            BusinessAiModelDownloadDetail = "";
        }
        else
        {
            IsBusinessAiModelDownloading = state.IsDownloading;
            IsQwenDownloading = state.IsDownloading;
            BusinessAiModelDownloadProgress = state.Progress ?? 0;
            BusinessAiModelDownloadDetail = detail;
            QwenDownloadProgress = state.Progress ?? 0;
            QwenDownloadDetail = detail;
            IsAiOcrModelDownloading = false;
            AiOcrModelDownloadDetail = "";
        }
        if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
            AiLastError = detail;
        DownloadQwenModelCommand.NotifyCanExecuteChanged();
        DeleteQwenModelCommand.NotifyCanExecuteChanged();
        DownloadAiOcrModelCommand.NotifyCanExecuteChanged();
        DeleteAiOcrModelCommand.NotifyCanExecuteChanged();
    }

    private string FormatDownloadState(SmartBpDownloadState state)
    {
        var status = ResolveLocalizedOrRaw(state.Status);
        if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
            return $"{status}: {state.ErrorMessage}";
        var percent = state.Progress is { } progress ? $"{progress:0.0}%" : "-";
        var bytes = state.BytesReceived is { } received
            ? $"{FormatBytes(received)} / {(state.TotalBytes is { } total ? FormatBytes(total) : "?")}"
            : "-";
        var speed = state.BytesPerSecond is { } bps ? $"{FormatBytes((long)bps)}/s" : "-";
        var eta = state.Eta is { } etaValue ? etaValue.ToString(@"mm\:ss") : "-";
        return $"{status}; {percent}; {state.CurrentFileName ?? "-"}; {bytes}; {speed}; ETA {eta}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:0.##} {units[index]}";
    }

    /// <summary>
    /// 识别策略下拉框选项。
    /// </summary>
    /// <param name="Strategy">识别策略值。</param>
    /// <param name="DisplayNameKey">本地化显示名称资源键。</param>
    public sealed record RecognitionStrategySelection(SmartBpRecognitionStrategy Strategy, string DisplayNameKey);

    /// <summary>一个可选择的 OCR Provider选项。</summary>
    /// <param name="Mode">持久化使用的Provider模式。</param>
    /// <param name="DisplayName">界面显示名称。</param>
    public sealed record OcrProviderSelection(SmartBpOcrProviderMode Mode, string DisplayName);

    /// <summary>SmartBP 界面下拉框中展示的 llama.cpp 运行时资产选项。</summary>
    public sealed partial class LlamaCppRuntimeAssetSelection : ObservableObject
    {
        /// <summary>根据清单中的资产定义初始化运行时选项。</summary>
        /// <param name="asset">清单资产定义。</param>
        public LlamaCppRuntimeAssetSelection(LlamaCppRuntimeAsset asset)
        {
            Id = asset.Id;
            DisplayName = asset.DisplayName;
            Architecture = asset.Architecture;
            Backend = asset.Backend;
            EntryExe = asset.EntryExe ?? "";
        }

        /// <summary>获取资产标识。</summary>
        public string Id { get; }
        /// <summary>获取显示名称。</summary>
        public string DisplayName { get; }
        /// <summary>获取 CPU 架构名称。</summary>
        public string Architecture { get; }
        /// <summary>获取后端名称。</summary>
        public string Backend { get; }
        /// <summary>获取入口可执行文件名。</summary>
        public string EntryExe { get; }

        /// <summary>获取或设置该运行时资产当前是否已安装。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusKey))]
        public partial bool IsInstalled { get; set; }

        /// <summary>获取当前安装状态对应的本地化资源键。</summary>
        public string StatusKey => IsInstalled ? "SmartBpAiStatusInstalled" : "SmartBpAiStatusNotInstalled";
    }

    /// <summary>SmartBP 界面中展示的可选择 Tesseract 语言数据选项。</summary>
    /// <param name="language">Tesseract 语言标识。</param>
    /// <param name="displayNameKey">显示名称的本地化资源键。</param>
    public sealed partial class TesseractLanguageSelection(string language, string displayNameKey) : ObservableObject
    {
        /// <summary>获取 Tesseract 语言标识。</summary>
        public string Language { get; } = language;

        /// <summary>获取用于显示的本地化资源键。</summary>
        public string DisplayNameKey { get; } = displayNameKey;

        /// <summary>获取或设置该语言是否被选中用于安装、删除和使用。</summary>
        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        /// <summary>获取或设置该语言数据文件是否已安装。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusKey))]
        public partial bool IsInstalled { get; set; }

        /// <summary>获取当前安装状态对应的本地化资源键。</summary>
        public string StatusKey => IsInstalled ? "SmartBpTesseractLanguageInstalled" : "SmartBpTesseractLanguageMissing";
    }
}
