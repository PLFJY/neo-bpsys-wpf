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
    /// <summary>Gets available recognition application modes.</summary>
    public IReadOnlyList<SmartBpRecognitionApplyMode> RecognitionApplyModes { get; } = Enum.GetValues<SmartBpRecognitionApplyMode>();

    /// <summary>Gets available built-in frames.</summary>
    public IReadOnlyList<SmartBpTestFrame> AiTestFrames { get; } =
    [
        new("ban-sur-16x9", "ban-sur-16x9.png", SmartBpRecognitionTask.BanSur),
        new("ban-hun-16x9", "ban-hun-16x9.png", SmartBpRecognitionTask.BanHun),
        new("pick-sur-16x9", "pick-sur-16x9.png", SmartBpRecognitionTask.PickSur),
        new("pick-hun-16x9", "pick-hun-16x9.png", SmartBpRecognitionTask.PickHun),
        new("character-distribution-16x9", "character-distribution-16x9.png", SmartBpRecognitionTask.CharacterDistribution)
    ];
    [ObservableProperty] private SmartBpTestFrame? _selectedAiTestFrame;
    [ObservableProperty] private string _qwenManifestStatus = "SmartBpAiStatusLoading";
    [ObservableProperty] private string _qwenModelProfile = "-";
    [ObservableProperty] private string _qwenMmprojProfile = "-";
    [ObservableProperty] private IReadOnlyList<QwenModelProfile> _qwenModelProfiles = [];
    [NotifyCanExecuteChangedFor(nameof(DownloadQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedQwenModelCommand))]
    [ObservableProperty] private QwenModelProfile? _selectedQwenModelProfile;
    [ObservableProperty] private string _currentQwenModelDisplayName = "";
    [ObservableProperty] private bool _isQwenInstalled;
    [NotifyCanExecuteChangedFor(nameof(DownloadQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedQwenModelCommand))]
    [ObservableProperty] private bool _isQwenDownloading;
    [NotifyCanExecuteChangedFor(nameof(DownloadQwenModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteQwenModelCommand))]
    [ObservableProperty] private bool _isSelectedQwenModelInstalled;
    [ObservableProperty] private IReadOnlyList<QwenModelProfile> _aiOcrModelProfiles = [];
    [NotifyCanExecuteChangedFor(nameof(DownloadAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedAiOcrModelCommand))]
    [ObservableProperty] private QwenModelProfile? _selectedAiOcrModelProfile;
    [ObservableProperty] private string _currentAiOcrModelDisplayName = "";
    [ObservableProperty] private string _aiOcrModelStatus = "-";
    [NotifyCanExecuteChangedFor(nameof(DownloadAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedAiOcrModelCommand))]
    [ObservableProperty] private bool _isAiOcrModelDownloading;
    [NotifyCanExecuteChangedFor(nameof(DownloadAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteAiOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedAiOcrModelCommand))]
    [ObservableProperty] private bool _isSelectedAiOcrModelInstalled;
    [ObservableProperty] private double _qwenDownloadProgress;
    [ObservableProperty] private string _qwenDownloadStatus = "-";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQwenDownloadDetail))]
    private string _qwenDownloadDetail = "";
    [ObservableProperty] private string _llamaServerExecutablePath = "";
    [ObservableProperty] private string _llamaServerStatus = "SmartBpAiStatusStopped";
    [NotifyCanExecuteChangedFor(nameof(StartAiPreviewLoopCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopAiPreviewLoopCommand))]
    [ObservableProperty] private bool _isAiRecognizing;
    [ObservableProperty] private bool _isAiPreviewLoopRunning;
    [ObservableProperty] private string _aiRawResponse = "";
    [ObservableProperty] private string _aiNormalizedResult = "";
    [ObservableProperty] private long _aiElapsedMilliseconds;
    [ObservableProperty] private int _aiRecommendedIntervalMilliseconds;
    [ObservableProperty] private string _aiLastError = "";
    [ObservableProperty] private string _aiDebugLogText = "";
    [ObservableProperty] private bool _isDebugLogEnabled = true;
    [ObservableProperty] private IReadOnlyList<SmartBpPromptProfile> _aiPromptProfiles = [];
    [ObservableProperty] private SmartBpPromptProfile? _selectedAiPromptProfile;
    [ObservableProperty] private IReadOnlyList<LlamaCppRuntimeAssetSelection> _llamaRuntimeAssets = [];
    [ObservableProperty] private LlamaCppRuntimeAssetSelection? _selectedLlamaRuntimeAsset;
    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RollbackLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceStopLlamaServerCommand))]
    [ObservableProperty] private bool _isLlamaRuntimeInstalled;
    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [ObservableProperty] private bool _isLlamaRuntimeDownloading;
    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RollbackLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceStopLlamaServerCommand))]
    [ObservableProperty] private bool _isLlamaServerRunning;
    [NotifyCanExecuteChangedFor(nameof(DownloadLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RollbackLlamaRuntimeCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopLlamaServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceStopLlamaServerCommand))]
    [ObservableProperty] private bool _isLlamaServerStarting;
    [ObservableProperty] private double _llamaRuntimeDownloadProgress;
    [ObservableProperty] private string _llamaRuntimeDownloadStatus = "-";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLlamaRuntimeDownloadDetail))]
    private string _llamaRuntimeDownloadDetail = "";
    [ObservableProperty] private string _managedLlamaServerExecutablePath = "-";
    [ObservableProperty] private string _llamaRuntimeUpdateStatus = "-";
    [ObservableProperty] private bool _enableAutoGuidanceSync;
    [ObservableProperty] private bool _enableAutoApplyRecognition;
    [ObservableProperty] private bool _enableAutoGuidancePageNavigation;
    [ObservableProperty] private SmartBpRecognitionApplyMode _recognitionApplyMode;
    [ObservableProperty] private bool _aiOneStepDelayedMode = true;
    [ObservableProperty] private int _aiUnknownPhaseTalentInferenceFrames = 2;
    [ObservableProperty] private bool _playBackfillAnimations;
    [ObservableProperty] private bool _useMultiImageSnapshotRequest;
    [ObservableProperty] private IReadOnlyList<RecognitionStrategySelection> _recognitionStrategies = [];
    [ObservableProperty] private RecognitionStrategySelection? _selectedRecognitionStrategy;
    [ObservableProperty] private bool _isOcrRecognitionEngine = true;
    [ObservableProperty] private bool _isAiQwenRecognitionEngine;
    [ObservableProperty] private bool _isPaddleRecognitionEngine = true;
    [ObservableProperty] private bool _isTesseractRecognitionEngine;
    [ObservableProperty] private bool _isRapidRecognitionEngine;
    [ObservableProperty] private bool _isBusinessAiModelVisible;
    [ObservableProperty] private bool _isOcrProviderCardVisible = true;
    [ObservableProperty] private bool _isAiOcrModelVisible;
    [ObservableProperty] private bool _enableOcrBpRecognition = true;
    [ObservableProperty] private int _recognitionIntervalMs;
    [ObservableProperty] private int _ocrRecognitionIntervalMs;
    [ObservableProperty] private int _ocrFieldStaleMilliseconds;
    [ObservableProperty] private int _ocrBackfillLookBehindSteps;
    [ObservableProperty] private bool _useOcrContactSheet = true;
    [ObservableProperty] private bool _enableOcrDebugOverlay;
    [ObservableProperty] private IReadOnlyList<OcrProviderSelection> _ocrProviders = [];
    [ObservableProperty] private OcrProviderSelection? _selectedOcrProvider;
    [ObservableProperty] private string _paddleOcrStatus = "-";
    [ObservableProperty] private string _tesseractOcrStatus = "-";
    [ObservableProperty] private bool _isTesseractDataDownloading;
    [ObservableProperty] private double _tesseractDownloadProgress;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTesseractDownloadDetail))]
    private string _tesseractDownloadDetail = "";
    [ObservableProperty] private string _tesseractLanguages = "chi_sim+eng";
    [ObservableProperty] private IReadOnlyList<TesseractLanguageSelection> _tesseractLanguageOptions = [];
    [ObservableProperty] private bool _enableTesseractOcr = true;
    [ObservableProperty] private int _tesseractDefaultPsm = 6;
    [ObservableProperty] private int _tesseractMaxPreprocessVariants = 3;
    [ObservableProperty] private IReadOnlyList<RapidOcrModelProfile> _rapidOcrModelProfiles = [];
    [ObservableProperty] private RapidOcrModelProfile? _selectedRapidOcrModelProfile;
    [ObservableProperty] private string _rapidOcrStatus = "-";
    [ObservableProperty] private string _rapidOcrModelDirectory = "-";
    [ObservableProperty] private string _rapidOcrInstalledVersion = "-";
    [ObservableProperty] private string _rapidOcrLatestVersion = "-";
    [ObservableProperty] private bool _isRapidOcrUpdateAvailable;
    [NotifyCanExecuteChangedFor(nameof(DownloadRapidOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRapidOcrModelCommand))]
    [ObservableProperty] private bool _isSelectedRapidOcrModelInstalled;
    [ObservableProperty] private string _rapidOcrInstallActionText = "-";
    [NotifyCanExecuteChangedFor(nameof(DownloadRapidOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRapidOcrModelCommand))]
    [ObservableProperty] private bool _isRapidOcrDownloading;
    [ObservableProperty] private double _rapidOcrDownloadProgress;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRapidOcrDownloadDetail))]
    private string _rapidOcrDownloadDetail = "";
    [ObservableProperty] private int _rapidOcrPadding;
    [ObservableProperty] private int _rapidOcrMaxSideLen = 1024;
    [ObservableProperty] private double _rapidOcrBoxScoreThreshold = .5;
    [ObservableProperty] private double _rapidOcrBoxThreshold = .3;
    [ObservableProperty] private double _rapidOcrUnclipRatio = 1.6;
    [ObservableProperty] private bool _rapidOcrUseAngleClassifier = true;
    [ObservableProperty] private bool _rapidOcrUsePreprocessingVariants;
    [ObservableProperty] private bool _allowSequentialSnapshotFallback;
    [ObservableProperty] private bool _useStrictCandidateEnumsInAutoSchema;
    [ObservableProperty] private int _phaseCropMaxImageWidth;
    [ObservableProperty] private int _contentCropMaxImageWidth;
    [ObservableProperty] private int _phaseMaxTokens;
    [ObservableProperty] private int _snapshotDeltaMaxTokens;
    [ObservableProperty] private int _phaseTransitionCommitHoldMilliseconds;
    [ObservableProperty] private int _phaseTransitionCommitHoldMaxMilliseconds;
    [ObservableProperty] private int _recognitionBackfillLookBehindSteps;
    [ObservableProperty] private int _recognitionFieldStaleMilliseconds;
    [ObservableProperty] private int _recognitionVisualBufferMilliseconds;
    [ObservableProperty] private int _llamaParallelSlots;
    [ObservableProperty] private int _llamaGpuLayers;
    [ObservableProperty] private bool _llamaFlashAttention;
    [ObservableProperty] private int _llamaBatchSize;
    [ObservableProperty] private int _llamaUBatchSize;
    [ObservableProperty] private string _aiStageDetectionResult = "-";
    [ObservableProperty] private string _aiGuidanceSnapshot = "-";
    [ObservableProperty] private string _aiCandidateOperations = "-";
    [ObservableProperty] private BitmapSource? _aiPhaseCropPreview;
    [ObservableProperty] private BitmapSource? _aiFocusedCropPreview;
    [ObservableProperty] private string _aiCropDebugInfo = "-";
    [ObservableProperty] private string _recognitionSpeedTestStatus = "-";
    [ObservableProperty] private string _currentRecognitionEngineText = "-";
    [ObservableProperty] private int _currentRecognitionIntervalMs;
    [ObservableProperty] private int _minimumRecognitionIntervalMs;
    [ObservableProperty] private string _recognitionIntervalEditHint = "-";
    [ObservableProperty] private string _aiSceneDiagnostics = "-";
    [ObservableProperty] private string _aiRequestMetrics = "-";
    [ObservableProperty] private bool _isRecognitionSpeedTesting;
    [ObservableProperty] private bool _isRecognitionIntervalEditable;
    [ObservableProperty] private string _aiGpuName = "not available";
    [ObservableProperty] private string _aiGpuUtilization = "not available";
    [ObservableProperty] private string _aiVramUsage = "not available";
    [ObservableProperty] private string _aiLlamaProcessId = "-";
    [ObservableProperty] private string _aiPerformanceUpdatedAt = "-";
    private SmartBpRecognitionLayoutProfile? _aiRegionProfile;

    /// <summary>Gets whether Qwen download details should be shown.</summary>
    public bool HasQwenDownloadDetail => !string.IsNullOrWhiteSpace(QwenDownloadDetail);

    /// <summary>Gets whether llama.cpp runtime download details should be shown.</summary>
    public bool HasLlamaRuntimeDownloadDetail => !string.IsNullOrWhiteSpace(LlamaRuntimeDownloadDetail);

    /// <summary>Gets whether Tesseract download details should be shown.</summary>
    public bool HasTesseractDownloadDetail => !string.IsNullOrWhiteSpace(TesseractDownloadDetail);

    /// <summary>Gets whether RapidOCR download details should be shown.</summary>
    public bool HasRapidOcrDownloadDetail => !string.IsNullOrWhiteSpace(RapidOcrDownloadDetail);

    private void InitializeAiRecognition()
    {
        SelectedAiTestFrame = AiTestFrames[0];
        RecognitionStrategies =
        [
            new(SmartBpRecognitionStrategy.PureOcr, "SmartBpRecognitionStrategyPureOcr"),
            new(SmartBpRecognitionStrategy.PureAi, "SmartBpRecognitionStrategyPureAi"),
            new(SmartBpRecognitionStrategy.AiWithOcr, "SmartBpRecognitionStrategyAiWithOcr"),
            new(SmartBpRecognitionStrategy.AiWithAiOcr, "SmartBpRecognitionStrategyAiWithAiOcr")
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
        RecognitionApplyMode = _recognitionSettingsService.Settings.RecognitionApplyMode;
        AiOneStepDelayedMode = _recognitionSettingsService.Settings.AiOneStepDelayedMode;
        AiUnknownPhaseTalentInferenceFrames = _recognitionSettingsService.Settings.AiUnknownPhaseTalentInferenceFrames;
        PlayBackfillAnimations = _recognitionSettingsService.Settings.PlayBackfillAnimations;
        UseMultiImageSnapshotRequest = _recognitionSettingsService.Settings.UseMultiImageSnapshotRequest;
        EnableOcrBpRecognition = _recognitionSettingsService.Settings.EnableOcrBpRecognition;
        RecognitionIntervalMs = _recognitionSettingsService.Settings.RecognitionIntervalMs;
        OcrRecognitionIntervalMs = _recognitionSettingsService.Settings.OcrRecognitionIntervalMs;
        OcrFieldStaleMilliseconds = _recognitionSettingsService.Settings.OcrFieldStaleMilliseconds;
        OcrBackfillLookBehindSteps = _recognitionSettingsService.Settings.OcrBackfillLookBehindSteps;
        UseOcrContactSheet = _recognitionSettingsService.Settings.UseOcrContactSheet;
        EnableOcrDebugOverlay = _recognitionSettingsService.Settings.EnableOcrDebugOverlay;
        OcrProviders =
        [
            new(SmartBpOcrProviderMode.Paddle, "PaddleOCR"),
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
        _aiPreviewTimer.Tick += async (_, _) => await RunAutomaticCurrentFrameCoreAsync();
        _aiPerformanceTimer.Tick += async (_, _) => await RefreshAiPerformanceAsync();
        _aiPerformanceTimer.Start();
        _qwenAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsQwenDownloading = state.IsDownloading; QwenDownloadProgress = state.Progress ?? 0; QwenDownloadStatus = ResolveLocalizedOrRaw(state.Status);
            IsAiOcrModelDownloading = state.IsDownloading;
            QwenDownloadDetail = FormatDownloadState(state);
            if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
                AiLastError = QwenDownloadDetail;
            if (!state.IsDownloading)
            {
                _ = RefreshSelectedQwenModelInstallStatusAsync();
                _ = RefreshSelectedAiOcrModelInstallStatusAsync();
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

    private async Task LoadAiRegionProfileAsync()
    {
        try
        {
            _aiRegionProfile = await _aiRegionProfileService.LoadAsync();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
    }

    [RelayCommand]
    private async Task OpenAiRecognitionRegionEditorAsync()
    {
        try
        {
            _aiRegionProfile ??= await _aiRegionProfileService.LoadAsync();

            var frame = _windowCaptureService.IsCapturing
                ? _windowCaptureService.GetCurrentFrame()
                : null;
            if (frame == null && SelectedAiTestFrame != null)
                frame = LoadTestFrame(SelectedAiTestFrame);
            if (frame == null)
            {
                await MessageBoxHelper.ShowInfoAsync(ResolveLocalizedOrRaw("SmartBpAiRegionEditorRequireFrame"));
                return;
            }

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
        catch (Exception ex) { AiLastError = ex.Message; }
    }

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

    private static readonly (string Id, string LabelKey)[] AiRegionEditorNodes =
    [
        ("phase_top", "SmartBpAiRegionPhaseTop"),
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
            // Llama.cpp assets are already loaded eagerly in InitializeAiRecognition; fall back if that failed.
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

    /// <summary>Loads bundled llama.cpp runtime assets without blocking the UI thread.</summary>
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

    /// <summary>Flushes buffered log messages to <see cref="AiDebugLogText"/> and clears the buffer.</summary>
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
        var strategy = SelectedRecognitionStrategy?.Strategy ?? _recognitionSettingsService.Settings.RecognitionStrategy;
        IsOcrRecognitionEngine = strategy is SmartBpRecognitionStrategy.PureOcr or SmartBpRecognitionStrategy.AiWithOcr;
        IsAiQwenRecognitionEngine = strategy is SmartBpRecognitionStrategy.PureAi or SmartBpRecognitionStrategy.AiWithOcr or SmartBpRecognitionStrategy.AiWithAiOcr;
        IsBusinessAiModelVisible = IsAiQwenRecognitionEngine;
        IsOcrProviderCardVisible = strategy is SmartBpRecognitionStrategy.PureOcr or SmartBpRecognitionStrategy.AiWithOcr;
        IsAiOcrModelVisible = strategy == SmartBpRecognitionStrategy.AiWithAiOcr;
        var provider = SelectedOcrProvider?.Mode ?? _recognitionSettingsService.Settings.SelectedOcrProviderMode;
        IsPaddleRecognitionEngine = IsOcrProviderCardVisible && provider == SmartBpOcrProviderMode.Paddle;
        IsTesseractRecognitionEngine = IsOcrProviderCardVisible && provider == SmartBpOcrProviderMode.Tesseract;
        IsRapidRecognitionEngine = IsOcrProviderCardVisible && provider == SmartBpOcrProviderMode.Rapid;
        RefreshRecognitionTimerInterval();
    }

    private void RefreshRecognitionTimerInterval()
    {
        var interval = _recognitionSettingsService.Settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr
            ? _recognitionSettingsService.Settings.OcrRecognitionIntervalMs
            : _recognitionSettingsService.Settings.RecognitionIntervalMs;
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
            if (_recognitionSettingsService.Settings.RecognitionStrategy != SmartBpRecognitionStrategy.PureOcr)
            {
                if (!_llamaServerManager.IsRunning) throw new InvalidOperationException("Start llama-server before testing AI recognition speed.");
                var watch = Stopwatch.StartNew();
                var result = await _autoRecognitionCoordinator.RunOneTickDryRunAsync(frame);
                watch.Stop();
                if (!string.IsNullOrWhiteSpace(result.Error)) throw new InvalidOperationException(result.Error);
                elapsed.Add(watch.ElapsedMilliseconds);
            }
            else
            {
                var watch = Stopwatch.StartNew();
                await _ocrBpRecognitionService.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(
                [
                    SmartBpRecognitionRegion.RightTop, SmartBpRecognitionRegion.LeftTop,
                    SmartBpRecognitionRegion.LeftBottom, SmartBpRecognitionRegion.RightBottom
                ]));
                watch.Stop();
                elapsed.Add(watch.ElapsedMilliseconds);
            }
            var minimum = checked((int)Math.Min(int.MaxValue, elapsed.Max() + 250));
            var settings = _recognitionSettingsService.Settings;
            if (settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr)
            {
                settings.MinimumOcrRecognitionIntervalMs = minimum;
                settings.OcrRecognitionIntervalMs = Math.Max(settings.OcrRecognitionIntervalMs, minimum);
                OcrRecognitionIntervalMs = settings.OcrRecognitionIntervalMs;
            }
            else
            {
                settings.MinimumAiRecognitionIntervalMs = minimum;
                settings.RecognitionIntervalMs = Math.Max(settings.RecognitionIntervalMs, minimum);
                RecognitionIntervalMs = settings.RecognitionIntervalMs;
            }
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
        return $"{s.RecognitionStrategy}|{s.SelectedOcrProviderMode}|{s.SelectedBusinessAiModelId}|{s.SelectedAiOcrModelId}|{s.UseSeparateAiOcrServer}|{s.BusinessAiServerPort}|{s.AiOcrServerPort}|{s.SelectedLlamaRuntimeId}|{s.PromptProfileId}|{s.UseOcrContactSheet}|{s.TesseractLanguages}|{s.TesseractDefaultPsm}|{s.TesseractMaxPreprocessVariants}|{s.SelectedRapidOcrModelId}|{s.RapidOcrPadding}|{s.RapidOcrMaxSideLen}|{s.RapidOcrBoxScoreThreshold}|{s.RapidOcrBoxThreshold}|{s.RapidOcrUnclipRatio}|{s.RapidOcrUseAngleClassifier}|{s.RapidOcrUsePreprocessingVariants}|{s.UseMultiImageSnapshotRequest}|{s.AllowSequentialSnapshotFallback}|{s.UseStrictCandidateEnumsInAutoSchema}|{s.PhaseCropMaxImageWidth}|{s.ContentCropMaxImageWidth}|{s.PhaseMaxTokens}|{s.SnapshotDeltaMaxTokens}|{s.PhaseTransitionCommitHoldMilliseconds}|{s.PhaseTransitionCommitHoldMaxMilliseconds}|{s.RecognitionVisualBufferMilliseconds}|{s.LlamaParallelSlots}|{s.LlamaGpuLayers}|{s.LlamaBatchSize}|{s.LlamaUBatchSize}|{s.LlamaFlashAttention}";
    }

    private void RefreshRecognitionSpeedTestValidity()
    {
        var settings = _recognitionSettingsService.Settings;
        IsRecognitionIntervalEditable = string.Equals(
            settings.LastRecognitionSpeedTestConfigurationHash,
            GetRecognitionSpeedFingerprint(), StringComparison.Ordinal);
        CurrentRecognitionEngineText = settings.RecognitionStrategy.ToString();
        CurrentRecognitionIntervalMs = settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr
            ? settings.OcrRecognitionIntervalMs : settings.RecognitionIntervalMs;
        MinimumRecognitionIntervalMs = settings.RecognitionStrategy == SmartBpRecognitionStrategy.PureOcr
            ? settings.MinimumOcrRecognitionIntervalMs : settings.MinimumAiRecognitionIntervalMs;
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
            await _qwenAssetManager.InstallAsync(SelectedQwenModelProfile.Id);
            await RefreshQwenStatusAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AiLastError = ex.ToString(); }
    }

    [RelayCommand] private void CancelQwenDownload() => _qwenAssetManager.Cancel();

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
        !IsQwenDownloading && SelectedQwenModelProfile != null && !IsSelectedQwenModelInstalled;

    private bool CanDeleteQwenModel() =>
        !IsQwenDownloading && !_llamaServerManager.IsRunning && SelectedQwenModelProfile != null && IsSelectedQwenModelInstalled;

    [RelayCommand(CanExecute = nameof(CanDownloadAiOcrModel))]
    private async Task DownloadAiOcrModelAsync()
    {
        if (SelectedAiOcrModelProfile == null) return;
        try
        {
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
        !IsAiOcrModelDownloading && SelectedAiOcrModelProfile != null && !IsSelectedAiOcrModelInstalled;

    private bool CanDeleteAiOcrModel() =>
        !IsAiOcrModelDownloading && !_llamaServerManager.IsRunning && SelectedAiOcrModelProfile != null && IsSelectedAiOcrModelInstalled;

    private bool CanSwitchSelectedAiOcrModel() =>
        !IsAiOcrModelDownloading &&
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

    private bool CanForceStopLlamaServer() =>
        IsLlamaServerRunning;
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
        ForceStopLlamaServerCommand.NotifyCanExecuteChanged();
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
    [RelayCommand] private async Task DetectStageFromSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try { await RunPhaseOnlyRecognitionCoreAsync(LoadTestFrame(SelectedAiTestFrame)); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private async Task DetectStageFromCurrentCaptureFrameAsync()
    {
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null) { AiLastError = "No capture frame is available."; return; }
        await RunPhaseOnlyRecognitionCoreAsync(frame);
    }
    [RelayCommand] private Task RunAutomaticOneTickAsync() => RunAutomaticCurrentFrameCoreAsync();
    [RelayCommand(CanExecute = nameof(CanStartAutomaticRecognition))]
    private async Task StartAiPreviewLoopAsync()
    {
        if (!_windowCaptureService.IsCapturing) { AiLastError = "Start capture before starting automatic recognition."; return; }
        var confirmed = await MessageBoxHelper.ShowConfirmAsync(
            ResolveLocalizedOrRaw("SmartBpAutoRecognitionStartConfirm"),
            ResolveLocalizedOrRaw("SmartBpAutoRecognitionStartTitle"),
            ResolveLocalizedOrRaw("Confirm"), ResolveLocalizedOrRaw("Cancel"));
        if (!confirmed) return;
        await _autoRecognitionCoordinator.StartAsync();
        IsAiPreviewLoopRunning = true;
        _aiPreviewTimer.Start();
        _autoRecognitionGlobalControl.Update(true, _ => StopAiPreviewLoopAsync());
        NotifyAutomaticRecognitionCommands();
    }

    [RelayCommand(CanExecute = nameof(CanStopAutomaticRecognition))]
    private async Task StopAiPreviewLoopAsync()
    {
        _aiPreviewTimer.Stop();
        await _autoRecognitionCoordinator.StopAsync();
        IsAiPreviewLoopRunning = false;
        _autoRecognitionGlobalControl.Update(false);
        NotifyAutomaticRecognitionCommands();
    }

    private bool CanStartAutomaticRecognition() => !IsAiPreviewLoopRunning && !IsAiRecognizing;
    private bool CanStopAutomaticRecognition() => IsAiPreviewLoopRunning || IsAiRecognizing;
    private void NotifyAutomaticRecognitionCommands()
    {
        StartAiPreviewLoopCommand.NotifyCanExecuteChanged();
        StopAiPreviewLoopCommand.NotifyCanExecuteChanged();
    }

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

    [RelayCommand] private void CancelTesseractDataDownload() => _tesseractDataAssetManager.Cancel();

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

    private async Task RefreshTesseractDataStatusAsync()
    {
        var status = await _tesseractDataAssetManager.GetStatusAsync();
        foreach (var option in TesseractLanguageOptions)
            option.IsInstalled = status.InstalledLanguages.Contains(option.Language, StringComparer.OrdinalIgnoreCase);
        RefreshOcrProviderStatuses();
    }

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

    [RelayCommand]
    private void CancelRapidOcrDownload() => _rapidOcrModelAssetManager.Cancel();

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

    private bool CanDownloadRapidOcrModel() =>
        SelectedRapidOcrModelProfile != null &&
        !IsRapidOcrDownloading &&
        (!IsSelectedRapidOcrModelInstalled || IsRapidOcrUpdateAvailable);

    private bool CanDeleteRapidOcrModel() =>
        SelectedRapidOcrModelProfile != null &&
        !IsRapidOcrDownloading &&
        IsSelectedRapidOcrModelInstalled;

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
    private async Task RecognizeCurrentFrameCoreAsync() { var frame = _windowCaptureService.GetCurrentFrame(); if (frame == null) { AiLastError = "No capture frame is available."; return; } await RunFullStrategyRecognitionCoreAsync(frame); }

    private async Task DetectStageCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            var dataUrl = await Task.Run(() => _smartBpImageEncoder.EncodeDataUrl(frame, _recognitionSettingsService.Settings.MaxImageWidth));
            var raw = await _llamaCppOpenAiClient.RecognizeAsync(dataUrl, SmartBpRecognitionTask.FullBpScan);
            var state = await Task.Run(() => SmartBpBusinessStateParser.Parse(raw));
            AiRawResponse = raw;
            AiStageDetectionResult = FormatBusinessState(state);
            AiParsedVisualResult = AiStageDetectionResult;
            RefreshGuidanceSnapshot();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunAutomaticCurrentFrameCoreAsync()
    {
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null) { AiLastError = "No capture frame is available."; return; }
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            var result = await _autoRecognitionCoordinator.RunOneTickAsync(frame);
            ApplyRegionGatedResult(result);
            if (result.SceneGate?.ShouldPauseAutomaticRecognition == true && IsAiPreviewLoopRunning)
            {
                await StopAiPreviewLoopAsync();
                AiSceneDiagnostics += Environment.NewLine + ResolveLocalizedOrRaw("SmartBpRecognitionPausedBpEnded");
            }
        }
        catch (OperationCanceledException) { }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
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
            var result = await _autoRecognitionCoordinator.RunFullRecognitionDebugAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunPhaseOnlyRecognitionCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try
        {
            var result = await _autoRecognitionCoordinator.RunPhaseOnlyDebugAsync(frame);
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
        AiRawResponse = result.RawJson;
        AiStageDetectionResult = result.BusinessState == null ? "-" : FormatBusinessState(result.BusinessState);
        AiGuidanceSnapshot = FormatGuidance(result.GuidanceSnapshot, result.GuidanceSync?.Reason);
        AiCandidateOperations = FormatAutomaticOperations(result);
        AiParsedVisualResult = AiStageDetectionResult;
        AiNormalizedResult = AiCandidateOperations;
        AiPhaseCropPreview = result.PhaseCrop?.Image;
        AiFocusedCropPreview = result.ContentCrops?.LastOrDefault()?.Image ?? result.FocusedCrop?.Image;
        AiCropDebugInfo = FormatCropDebugInfo(result);
        AiSceneDiagnostics = result.SceneGate == null ? "-" :
            $"Scene: {result.SceneGate.Scene}{Environment.NewLine}BP recognition allowed: {result.SceneGate.IsBpRecognitionAllowed}{Environment.NewLine}Character operations allowed: {result.SceneGate.IsCharacterOperationAllowed}{Environment.NewLine}Action: {(result.SceneGate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}{Environment.NewLine}Reason: {result.SceneGate.Reason}";
        var metrics = _llamaCppOpenAiClient.LastResponseMetrics;
        AiRequestMetrics = metrics == null ? "not available" :
            $"AI request elapsed: {metrics.ElapsedMilliseconds}ms; completion tokens: {metrics.CompletionTokens?.ToString() ?? "not available"}; output tokens/sec: {metrics.TokensPerSecond?.ToString("0.##") ?? "not available"}; finish_reason: {_llamaCppOpenAiClient.LastFinishReason ?? "not available"}";
        AiLastError = result.Error ?? "";
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
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try { var result = await _aiRecognitionService.RecognizeAsync(frame, task); AiRawResponse = result.RawResponse; AiParsedVisualResult = result.ParsedVisualSummary; AiNormalizedResult = result.ResolvedCharacterSummary; AiElapsedMilliseconds = result.ElapsedMilliseconds; AiRecommendedIntervalMilliseconds = result.RecommendedIntervalMilliseconds; AiLastError = result.Error ?? ""; }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    [ObservableProperty] private string _aiParsedVisualResult = "";

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
        !IsQwenDownloading &&
        SelectedQwenModelProfile != null &&
        !string.Equals(SelectedQwenModelProfile.Id, _recognitionSettingsService.Settings.SelectedBusinessAiModelId, StringComparison.Ordinal);

    private async Task SwitchQwenModelAsync(QwenModelProfile value)
    {
        _isSwitchingQwenModel = true;
        var oldId = _recognitionSettingsService.Settings.SelectedBusinessAiModelId;
        try
        {
            var restart = _llamaServerManager.IsRunning;
            if (restart)
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
                await _llamaServerManager.StopAsync();
            }
            _recognitionSettingsService.Settings.SelectedBusinessAiModelId = value.Id;
            _recognitionSettingsService.Settings.SelectedQwenModelId = value.Id;
            RefreshRecognitionSpeedTestValidity();
            await SaveQwenSelectionAsync();
            CurrentQwenModelDisplayName = string.Format(ResolveLocalizedOrRaw("SmartBpCurrentQwenModelFormat"), value.DisplayName);
            IsQwenInstalled = await _qwenAssetManager.IsInstalledAsync();
            await RefreshSelectedQwenModelInstallStatusAsync();
            SwitchSelectedQwenModelCommand.NotifyCanExecuteChanged();
            if (restart && IsQwenInstalled)
                await _llamaServerManager.StartAsync();
            else if (!IsQwenInstalled)
                AiLastError = ResolveLocalizedOrRaw("SmartBpAiModelDownloadRequired");
        }
        catch (Exception ex) { AiLastError = ex.Message; }
        finally { _isSwitchingQwenModel = false; }
    }

    private async Task SwitchAiOcrModelAsync(QwenModelProfile value)
    {
        _isSwitchingAiOcrModel = true;
        var oldId = _recognitionSettingsService.Settings.SelectedAiOcrModelId;
        try
        {
            if (_llamaServerManager.IsRunning)
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
                await _llamaServerManager.StopAsync();
            }

            _recognitionSettingsService.Settings.SelectedAiOcrModelId = value.Id;
            RefreshRecognitionSpeedTestValidity();
            await _recognitionSettingsService.SaveAsync();
            CurrentAiOcrModelDisplayName = value.DisplayName;
            await RefreshSelectedAiOcrModelInstallStatusAsync();
            if (!IsSelectedAiOcrModelInstalled)
                AiLastError = ResolveLocalizedOrRaw("SmartBpAiModelDownloadRequired");
        }
        catch (Exception ex) { AiLastError = ex.Message; }
        finally { _isSwitchingAiOcrModel = false; }
    }

    partial void OnSelectedLlamaRuntimeAssetChanged(LlamaCppRuntimeAssetSelection? value)
    {
        if (value == null || value.Id == _recognitionSettingsService.Settings.SelectedLlamaRuntimeId) return;
        if (_llamaServerManager.IsRunning)
        {
            // Revert to the current selection
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

    partial void OnRecognitionApplyModeChanged(SmartBpRecognitionApplyMode value)
    {
        _recognitionSettingsService.Settings.RecognitionApplyMode = value;
        _ = _recognitionSettingsService.SaveAsync();
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
    /// Recognition strategy combo-box item.
    /// </summary>
    /// <param name="Strategy">Strategy value.</param>
    /// <param name="DisplayNameKey">Localized display name key.</param>
    public sealed record RecognitionStrategySelection(SmartBpRecognitionStrategy Strategy, string DisplayNameKey);

    /// <summary>One selectable OCR provider.</summary>
    /// <param name="Mode">Persisted provider mode.</param>
    /// <param name="DisplayName">Display name.</param>
    public sealed record OcrProviderSelection(SmartBpOcrProviderMode Mode, string DisplayName);

    /// <summary>Selectable llama.cpp runtime asset shown in the SmartBP UI combobox.</summary>
    public sealed partial class LlamaCppRuntimeAssetSelection : ObservableObject
    {
        /// <summary>Initializes a new instance from a manifest asset.</summary>
        /// <param name="asset">The manifest asset definition.</param>
        public LlamaCppRuntimeAssetSelection(LlamaCppRuntimeAsset asset)
        {
            Id = asset.Id;
            DisplayName = asset.DisplayName;
            Architecture = asset.Architecture;
            Backend = asset.Backend;
            EntryExe = asset.EntryExe ?? "";
        }

        /// <summary>Gets the asset identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the display name.</summary>
        public string DisplayName { get; }
        /// <summary>Gets the CPU architecture.</summary>
        public string Architecture { get; }
        /// <summary>Gets the backend name.</summary>
        public string Backend { get; }
        /// <summary>Gets the entry executable filename.</summary>
        public string EntryExe { get; }

        /// <summary>Gets or sets whether this runtime asset is currently installed.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusKey))]
        private bool _isInstalled;

        /// <summary>Gets the localization key for the current install status.</summary>
        public string StatusKey => IsInstalled ? "SmartBpAiStatusInstalled" : "SmartBpAiStatusNotInstalled";
    }

    /// <summary>Selectable Tesseract language data option shown in the SmartBP UI.</summary>
    /// <param name="language">Tesseract language identifier.</param>
    /// <param name="displayNameKey">Localization key for the display name.</param>
    public sealed partial class TesseractLanguageSelection(string language, string displayNameKey) : ObservableObject
    {
        /// <summary>Gets the Tesseract language identifier.</summary>
        public string Language { get; } = language;

        /// <summary>Gets the localization key for display.</summary>
        public string DisplayNameKey { get; } = displayNameKey;

        /// <summary>Gets or sets whether this language is selected for install, delete, and use.</summary>
        [ObservableProperty] private bool _isSelected;

        /// <summary>Gets or sets whether this language data file is installed.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusKey))]
        private bool _isInstalled;

        /// <summary>Gets the localization key for the current install status.</summary>
        public string StatusKey => IsInstalled ? "SmartBpTesseractLanguageInstalled" : "SmartBpTesseractLanguageMissing";
    }
}
