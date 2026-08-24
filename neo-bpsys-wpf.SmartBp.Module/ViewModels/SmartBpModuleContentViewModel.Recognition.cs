using System.Text;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
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
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SmartBpModuleContentViewModel
{
    private readonly DispatcherTimer _previewTimer = new();
    private readonly DispatcherTimer _frameSamplingTimer = new();
    private int _recognitionBusy;
    private bool _isAutomaticRecognitionStopPendingAfterQueueDrain;
    private int _automaticRecognitionUnavailableFrameCount;
    /// <summary>获取可用的内置测试帧。</summary>
    public IReadOnlyList<SmartBpTestFrame> TestFrames { get; } =
    [
        new("ban-sur-16x9", "ban-sur-16x9.png", SmartBpRecognitionTask.BanSur),
        new("ban-hun-16x9", "ban-hun-16x9.png", SmartBpRecognitionTask.BanHun),
        new("pick-sur-16x9", "pick-sur-16x9.png", SmartBpRecognitionTask.PickSur),
        new("pick-hun-16x9", "pick-hun-16x9.png", SmartBpRecognitionTask.PickHun),
        new("character-distribution-16x9", "character-distribution-16x9.png", SmartBpRecognitionTask.CharacterDistribution)
    ];
    [ObservableProperty]
    public partial SmartBpTestFrame? SelectedTestFrame { get; set; }

    // 已移除的本地视觉模型设置保留在历史配置兼容层中，不再暴露为 SmartBP 页面状态。
    [NotifyCanExecuteChangedFor(nameof(StartPreviewLoopCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopPreviewLoopCommand))]
    [ObservableProperty]
    public partial bool IsRecognizing { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewLoopRunning { get; set; }

    [ObservableProperty]
    public partial string RawResponse { get; set; } = "";

    [ObservableProperty]
    public partial string NormalizedResult { get; set; } = "";

    [ObservableProperty]
    public partial long ElapsedMilliseconds { get; set; }

    [ObservableProperty]
    public partial int RecommendedIntervalMilliseconds { get; set; }

    [ObservableProperty]
    public partial string LastError { get; set; } = "";

    [ObservableProperty]
    public partial string DebugLogText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsDebugLogEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableAutoGuidanceSync { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableAutoApplyRecognition { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableAutoGuidancePageNavigation { get; set; }

    [ObservableProperty]
    public partial string LastSmartBpProgressDiagnosis { get; set; } = "-";

    [ObservableProperty]
    public partial bool UseMultiImageSnapshotRequest { get; set; }

    [ObservableProperty]
    public partial bool IsOcrRecognitionEngine { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPaddleRecognitionEngine { get; set; } = true;

    [ObservableProperty]
    public partial bool IsTesseractRecognitionEngine { get; set; }

    [ObservableProperty]
    public partial bool IsRapidRecognitionEngine { get; set; }

    [ObservableProperty]
    public partial bool EnableOcrBpRecognition { get; set; } = true;

    [ObservableProperty]
    public partial int RecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial int OcrRecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial int OcrBackfillLookBehindSteps { get; set; } = 2;

    [ObservableProperty]
    public partial int RecognitionTransitionLookBehindMilliseconds { get; set; } = 800;

    [ObservableProperty]
    public partial double RecognitionTransitionReplayMinimumConfidence { get; set; } = .95;

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

    /// <summary>
    /// 获取或设置 Tesseract 数据是否正在下载。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPauseTesseractDownload))]
    public partial bool IsTesseractDataDownloading { get; set; }

    /// <summary>
    /// 获取或设置 Tesseract 数据下载是否已暂停。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPauseTesseractDownload))]
    public partial bool IsTesseractDownloadPaused { get; set; }

    /// <summary>当前是否可以暂停 Tesseract 数据下载。</summary>
    public bool CanPauseTesseractDownload => IsTesseractDataDownloading && !IsTesseractDownloadPaused;

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

    /// <summary>
    /// 获取或设置 RapidOCR 模型下载是否正在进行。
    /// </summary>
    [NotifyCanExecuteChangedFor(nameof(DownloadRapidOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRapidOcrModelCommand))]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPauseRapidOcrDownload))]
    public partial bool IsRapidOcrDownloading { get; set; }

    /// <summary>
    /// 获取或设置 RapidOCR 模型下载是否已暂停。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPauseRapidOcrDownload))]
    public partial bool IsRapidOcrDownloadPaused { get; set; }

    /// <summary>当前是否可以暂停 RapidOCR 模型下载。</summary>
    public bool CanPauseRapidOcrDownload => IsRapidOcrDownloading && !IsRapidOcrDownloadPaused;

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
    public partial int RecognitionVisualBufferMilliseconds { get; set; }

    [ObservableProperty]
    public partial string StageDetectionResult { get; set; } = "-";

    [ObservableProperty]
    public partial string GuidanceSnapshot { get; set; } = "-";

    [ObservableProperty]
    public partial string CandidateOperations { get; set; } = "-";

    [ObservableProperty]
    public partial BitmapSource? PhaseCropPreview { get; set; }

    [ObservableProperty]
    public partial BitmapSource? FocusedCropPreview { get; set; }

    [ObservableProperty]
    public partial string CropDebugInfo { get; set; } = "-";

    [ObservableProperty]
    public partial string RecognitionSpeedTestStatus { get; set; } = "-";

    [ObservableProperty]
    public partial int CurrentRecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial int MinimumRecognitionIntervalMs { get; set; }

    [ObservableProperty]
    public partial string RecognitionIntervalEditHint { get; set; } = "-";

    [ObservableProperty]
    public partial string SceneDiagnostics { get; set; } = "-";

    [ObservableProperty]
    public partial string RequestMetrics { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsRecognitionSpeedTesting { get; set; }

    [ObservableProperty]
    public partial bool IsRecognitionIntervalEditable { get; set; }

    [ObservableProperty]
    public partial string GpuName { get; set; } = "not available";

    [ObservableProperty]
    public partial string GpuUtilization { get; set; } = "not available";

    [ObservableProperty]
    public partial string VramUsage { get; set; } = "not available";


    [ObservableProperty]
    public partial string PerformanceUpdatedAt { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugStrategySummary { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugModeSummary { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugFinalBusinessState { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugPhaseScene { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugOcrRawLines { get; set; } = "-";


    [ObservableProperty]
    public partial string DebugParsedState { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugMergeLog { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugCandidateOperations { get; set; } = "-";

    [ObservableProperty]
    public partial string DebugTiming { get; set; } = "-";

    [ObservableProperty]
    public partial string RecognitionDebugLogText { get; set; } = "-";

    [ObservableProperty]
    public partial bool IsRecognitionDebugLogAutoScrollEnabled { get; set; } = true;

    // 已移除本地视觉模型服务状态展示。
    private SmartBpRecognitionLayoutProfile? _regionProfile;

    /// <summary>是否显示 Tesseract 语言数据下载详情。</summary>
    public bool HasTesseractDownloadDetail => !string.IsNullOrWhiteSpace(TesseractDownloadDetail);

    /// <summary>是否显示 RapidOCR 模型下载详情。</summary>
    public bool HasRapidOcrDownloadDetail => !string.IsNullOrWhiteSpace(RapidOcrDownloadDetail);

    /// <summary>
    /// 初始化 AI/BP 自动识别页面状态、下载事件、调试日志缓冲和后台刷新计时器。
    /// </summary>
    private void InitializeRecognition()
    {
        SelectedTestFrame = TestFrames[0];
        RefreshRecognitionEngineVisibility();
        EnableAutoGuidanceSync = _recognitionSettingsService.Settings.EnableAutoGuidanceSync;
        EnableAutoApplyRecognition = _recognitionSettingsService.Settings.EnableAutoApplyRecognition;
        EnableAutoGuidancePageNavigation = _recognitionSettingsService.Settings.EnableAutoGuidancePageNavigation;
        UseMultiImageSnapshotRequest = _recognitionSettingsService.Settings.UseMultiImageSnapshotRequest;
        EnableOcrBpRecognition = _recognitionSettingsService.Settings.EnableOcrBpRecognition;
        RecognitionIntervalMs = _recognitionSettingsService.Settings.RecognitionIntervalMs;
        OcrRecognitionIntervalMs = _recognitionSettingsService.Settings.OcrRecognitionIntervalMs;
        OcrBackfillLookBehindSteps = _recognitionSettingsService.Settings.OcrBackfillLookBehindSteps;
        RecognitionTransitionLookBehindMilliseconds = _recognitionSettingsService.Settings.RecognitionTransitionLookBehindMilliseconds;
        RecognitionTransitionReplayMinimumConfidence = _recognitionSettingsService.Settings.RecognitionTransitionReplayMinimumConfidence;
        UseOcrContactSheet = _recognitionSettingsService.Settings.UseOcrContactSheet;
        EnableOcrDebugOverlay = _recognitionSettingsService.Settings.EnableOcrDebugOverlay;
        RebuildOcrProviderSelections();
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
        RecognitionVisualBufferMilliseconds = _recognitionSettingsService.Settings.RecognitionVisualBufferMilliseconds;
        RefreshRecognitionTimerInterval();
        RefreshRecognitionSpeedTestValidity();
        // 自动循环 Tick 只负责调度当前帧识别，具体阶段门禁和写回保护在 coordinator 内完成。
        _previewTimer.Tick += async (_, _) => await RunAutomaticCurrentFrameCoreAsync();
        _frameSamplingTimer.Interval = TimeSpan.FromMilliseconds(
            Math.Clamp(_recognitionSettingsService.Settings.RecognitionSamplingIntervalMilliseconds, 50, 1000));
        _frameSamplingTimer.Tick += (_, _) =>
        {
            if (!IsPreviewLoopRunning || !_windowCaptureService.IsCapturing)
                return;
            var sampledFrame = _windowCaptureService.GetCurrentFrame();
            if (sampledFrame is not null)
                _autoRecognitionCoordinator.SampleFrame(sampledFrame);
        };
        _rapidOcrModelAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsRapidOcrDownloading = state.IsDownloading;
            IsRapidOcrDownloadPaused = state.IsPaused;
            RapidOcrDownloadProgress = state.Progress ?? 0;
            RapidOcrDownloadDetail = state.IsDownloading || !string.IsNullOrWhiteSpace(state.ErrorMessage)
                ? FormatDownloadState(state)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(state.ErrorMessage)) LastError = RapidOcrDownloadDetail;
            if (!state.IsDownloading) _ = RefreshRapidOcrStatusAsync();
        });
        _debugLog.MessageWritten += (_, message) =>
        {
            lock (_debugLogBufferLock)
                _debugLogBuffer.AppendFormat("[{0:HH:mm:ss.fff}] [{1}] {2}{3}",
                    message.Timestamp, message.Source, message.Message, Environment.NewLine);
        };
        _debugLogFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _debugLogFlushTimer.Tick += (_, _) => FlushDebugLogBuffer();
        _debugLogFlushTimer.Start();
        _debugLog.Write("SmartBP", "Recognition diagnostics initialized.");
        _tesseractDataAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsTesseractDataDownloading = state.IsDownloading;
            IsTesseractDownloadPaused = state.IsPaused;
            TesseractDownloadProgress = state.Progress ?? 0;
            TesseractDownloadDetail = FormatDownloadState(state);
            if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
                LastError = TesseractDownloadDetail;
            if (!state.IsDownloading) _ = RefreshTesseractDataStatusAsync();
        });
        _ = LoadRegionProfileAsync();
    }

    /// <summary>
    /// 从配置服务加载 BP 自动识别区域配置档。
    /// </summary>
    /// <returns>加载任务。</returns>
    private async Task LoadRegionProfileAsync()
    {
        try
        {
            _regionProfile = await _aiRegionProfileService.LoadAsync();
        }
        catch (Exception ex) { LastError = ex.Message; }
    }

    /// <summary>
    /// 打开 BP 自动识别区域编辑器，并保存用户覆盖配置。
    /// </summary>
    /// <returns>编辑流程完成后的任务。</returns>
    [RelayCommand]
    private async Task OpenRecognitionRegionEditorAsync()
    {
        try
        {
            _regionProfile ??= await _aiRegionProfileService.LoadAsync();

            var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: false);
            if (frame == null)
                return;

            var editor = new RegionEditorWindow(frame, BuildRegionEditorLayout(_regionProfile))
            {
                Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                        ?? Application.Current?.MainWindow
            };
            if (editor.ShowDialog() != true || editor.ResultLayout == null)
                return;

            ApplyRegionEditorLayout(_regionProfile, editor.ResultLayout);
            await _aiRegionProfileService.SaveUserOverrideAsync(_regionProfile);
            await LoadRegionProfileAsync();
            CropDebugInfo = ResolveLocalizedOrRaw("SmartBpRegionProfileSaved");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            await MessageBoxHelper.ShowErrorAsync(FormatLocalizedDetail("SmartBpOperationFailedFormat", ex.Message));
        }
    }

    /// <summary>
    /// 将 BP 自动识别区域配置重置为模块内置默认值。
    /// </summary>
    /// <returns>重置流程完成后的任务。</returns>
    [RelayCommand]
    private async Task ResetRecognitionLayoutProfileAsync()
    {
        try
        {
            await _aiRegionProfileService.ResetUserOverrideAsync();
            await LoadRegionProfileAsync();
            CropDebugInfo = ResolveLocalizedOrRaw("SmartBpRegionProfileReset");
            await MessageBoxHelper.ShowInfoAsync(
                ResolveLocalizedOrRaw("SmartBpRegionProfileReset"),
                ResolveLocalizedOrRaw("SmartBpNotification"),
                ResolveLocalizedOrRaw("SmartBpClose"));
        }
        catch (Exception ex) { LastError = ex.Message; }
    }

    /// <summary>
    /// 打开 BP 自动识别区域调整示例图示，图片宽度与主窗口一致并按原始比例自适应高度。
    /// </summary>
    /// <returns>展示流程完成后的任务。</returns>
    [RelayCommand]
    private async Task ShowBpRecognitionRegionExampleAsync()
    {
        try
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null)
                return;

            var imagePath = Path.Combine(_smartBpModuleStorage.ModuleRoot, "Resources", "SmartBp", "BpRecognitionRegionExample.png");
            if (!File.Exists(imagePath))
            {
                LastError = imagePath;
                await MessageBoxHelper.ShowErrorAsync(FormatLocalizedDetail("SmartBpOperationFailedFormat", imagePath));
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath);
            bitmap.EndInit();
            bitmap.Freeze();

            var windowWidth = mainWindow.ActualWidth > 0 ? mainWindow.ActualWidth : mainWindow.Width;
            var imageHeight = bitmap.PixelWidth > 0
                ? windowWidth * bitmap.PixelHeight / bitmap.PixelWidth
                : windowWidth * 9.0 / 16.0;

            var window = new FluentWindow
            {
                Title = ResolveLocalizedOrRaw("SmartBpBpRecognitionRegionExampleDialogTitle"),
                Width = windowWidth,
                Height = imageHeight + 35,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = mainWindow,
                Icon = mainWindow.Icon,
                ExtendsContentIntoTitleBar = true,
                ResizeMode = ResizeMode.CanMinimize
            };
            WindowChrome.SetWindowChrome(window, new WindowChrome
            {
                CaptionHeight = 35,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });

            var baseGrid = new Grid();
            baseGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            baseGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar = new TitleBar();
            Grid.SetRow(titleBar, 0);

            var image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(image, 1);

            baseGrid.Children.Add(titleBar);
            baseGrid.Children.Add(image);

            window.Content = baseGrid;
            window.Show();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            await MessageBoxHelper.ShowErrorAsync(FormatLocalizedDetail("SmartBpOperationFailedFormat", ex.Message));
        }
    }

    /// <summary>
    /// 将归一化 BP 识别区域配置档转换为通用区域编辑器使用的百分比布局。
    /// </summary>
    /// <param name="profile">BP 自动识别区域配置档。</param>
    /// <returns>区域编辑器布局。</returns>
    private RegionLayoutDefinition BuildRegionEditorLayout(SmartBpRecognitionLayoutProfile profile)
    {
        var layout = RegionLayoutDefinition.Builder(ResolveLocalizedOrRaw("SmartBpRegionEditor"));
        foreach (var (id, labelKey) in RegionEditorNodes)
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
    private static void ApplyRegionEditorLayout(
        SmartBpRecognitionLayoutProfile profile,
        RegionLayoutDefinition editedLayout)
    {
        var nodes = editedLayout.Roots.ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach (var (id, _) in RegionEditorNodes)
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
    private static readonly (string Id, string LabelKey)[] RegionEditorNodes =
    [
        ("phase_top", "SmartBpRegionPhaseTop"),
        ("top_center_status", "SmartBpRegionTopCenterStatus"),
        ("top_left_status", "SmartBpRegionTopLeftStatus"),
        ("left_top", "SmartBpRegionLeftTop"),
        ("right_top", "SmartBpRegionRightTop"),
        ("left_bottom", "SmartBpRegionLeftBottom"),
        ("right_bottom", "SmartBpRegionRightBottom")
    ];

    // 本模块仅提供 OCR 识别；下列本地视觉模型管理实现已停止注册。
    [RelayCommand]
    private void ClearDebugLog()
    {
        lock (_debugLogBufferLock)
            _debugLogBuffer.Clear();
        DebugLogText = "";
    }

    /// <summary>
    /// 从 DEBUG 区触发赛后数据识别，并将识别结果写入共享对局数据。
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RecognizePostGameDataAsync()
    {
        IsRecognizingPostGameData = true;
        try
        {
            await _smartBpService.AutoFillGameDataAsync();
        }
        finally
        {
            IsRecognizingPostGameData = false;
        }
    }

    [ObservableProperty]
    public partial bool IsRecognizingPostGameData { get; set; }

    [ObservableProperty]
    public partial int PostGameRecognitionProgressPercent { get; set; }

    [ObservableProperty]
    public partial string PostGameRecognitionStageText { get; set; } = string.Empty;

    /// <summary>
    /// 将赛后数据识别进度快照应用到可观察属性，驱动进度条与阶段文本。
    /// 必须在 UI 线程调用。
    /// </summary>
    /// <param name="progress">进度快照。</param>
    private void ApplyPostGameRecognitionProgress(PostGameRecognitionProgress progress)
    {
        PostGameRecognitionProgressPercent = progress.Percent;
        PostGameRecognitionStageText = progress.StageText;
    }

    /// <summary>将缓冲的日志消息写入 <see cref="DebugLogText"/> 并清空缓冲区。</summary>
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
        var newText = DebugLogText + batch;
        if (newText.Length > maximumCharacters)
        {
            var firstLineBreak = newText.IndexOf(Environment.NewLine, newText.Length - maximumCharacters, StringComparison.Ordinal);
            newText = firstLineBreak >= 0 ? newText[(firstLineBreak + Environment.NewLine.Length)..] : newText[^maximumCharacters..];
        }
        DebugLogText = newText;
    }

    partial void OnIsDebugLogEnabledChanged(bool value)
    {
        _debugLog.IsEnabled = value;
    }

    private void RefreshRecognitionEngineVisibility()
    {
        var provider = SelectedOcrProvider?.Mode ?? _recognitionSettingsService.Settings.SelectedOcrProviderMode;
        IsPaddleRecognitionEngine = provider == SmartBpOcrProviderMode.Paddle;
        IsTesseractRecognitionEngine = provider == SmartBpOcrProviderMode.Tesseract;
        IsRapidRecognitionEngine = provider == SmartBpOcrProviderMode.Rapid;
        RefreshRecognitionTimerInterval();
    }

    private void RefreshRecognitionTimerInterval()
    {
        var interval = _recognitionSettingsService.Settings.OcrRecognitionIntervalMs;
        _previewTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(interval, 100, 5000));
    }

    [RelayCommand]
    private async Task TestRecognitionSpeedAsync()
    {
        if (IsRecognitionSpeedTesting) return;
        IsRecognitionSpeedTesting = true;
        try
        {
            var elapsed = new List<long>();
            var testFrame = SelectedTestFrame ?? TestFrames.FirstOrDefault();
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
        return $"{s.SelectedOcrProviderMode}|{s.UseOcrContactSheet}|{s.TesseractLanguages}|{s.TesseractDefaultPsm}|{s.TesseractMaxPreprocessVariants}|{s.SelectedRapidOcrModelId}|{s.RapidOcrPadding}|{s.RapidOcrMaxSideLen}|{s.RapidOcrBoxScoreThreshold}|{s.RapidOcrBoxThreshold}|{s.RapidOcrUnclipRatio}|{s.RapidOcrUseAngleClassifier}|{s.RapidOcrUsePreprocessingVariants}";
    }

    private void RefreshRecognitionSpeedTestValidity()
    {
        var settings = _recognitionSettingsService.Settings;
        IsRecognitionIntervalEditable = string.Equals(
            settings.LastRecognitionSpeedTestConfigurationHash,
            GetRecognitionSpeedFingerprint(), StringComparison.Ordinal);
        CurrentRecognitionIntervalMs = settings.OcrRecognitionIntervalMs;
        MinimumRecognitionIntervalMs = settings.MinimumOcrRecognitionIntervalMs;
        RecognitionIntervalEditHint = IsRecognitionIntervalEditable
            ? ResolveLocalizedOrRaw("SmartBpRecognitionIntervalReady")
            : ResolveLocalizedOrRaw("SmartBpRecognitionIntervalRequiresSpeedTest");
    }
    [RelayCommand] private async Task RecognizeSelectedTestFrameAsync()
    {
        if (SelectedTestFrame == null) return;
        try
        {
            var frame = LoadTestFrame(SelectedTestFrame);
            await RunFullStrategyRecognitionCoreAsync(frame);
        }
        catch (Exception ex) { LastError = ex.Message; }
    }
    [RelayCommand] private Task RecognizeCurrentCaptureFrameAsync() => RecognizeCurrentFrameCoreAsync();
    [RelayCommand]
    private async Task ForceSyncGameStateAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true, useInfoBar: true);
        if (frame == null) return;
        try
        {
            IsRecognizing = true;
            // 强制同步使用一次独立 OCR 结果直接与宿主状态对账，不写入自动识别 Observation Buffer。
            var snapshot = await _autoRecognitionCoordinator.RecognizeFullBpSnapshotAsync(frame, isDryRun: true);
            if (snapshot.BusinessState == null)
            {
                var error = LocalizeProgressSyncMessage(snapshot.Error) ?? ResolveLocalizedOrRaw("SmartBpProgressForceSyncNoSnapshot");
                LastSmartBpProgressDiagnosis = error;
                _infoBarService.ShowWarningInfoBar(error);
                return;
            }

            var result = await _gameStateSyncService.ForceSyncAsync(snapshot.BusinessState);
            ApplyRegionGatedResult(snapshot);
            LastSmartBpProgressDiagnosis = FormatGameStateSyncResult(result);
            CandidateOperations = string.Join(Environment.NewLine, result.Diagnostics);
            NormalizedResult = CandidateOperations;
            ShowForceSyncGameStateInfoBar(result);
        }
        catch (OperationCanceledException)
        {
            var message = ResolveLocalizedOrRaw("QueueCanceled");
            LastSmartBpProgressDiagnosis = message;
            _infoBarService.ShowInformationalInfoBar(message);
        }
        catch (Exception ex)
        {
            LastError = ex.ToString();
            var message = FormatLocalizedDetail("SmartBpProgressForceSyncFailedFormat", ex.Message);
            LastSmartBpProgressDiagnosis = message;
            _infoBarService.ShowErrorInfoBar(message);
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    [RelayCommand] private async Task RecognizeIncrementalSelectedTestFrameAsync()
    {
        if (SelectedTestFrame == null) return;
        try { await RunIncrementalRecognitionCoreAsync(LoadTestFrame(SelectedTestFrame)); }
        catch (Exception ex) { LastError = ex.Message; }
    }
    [RelayCommand] private async Task RecognizeIncrementalCurrentCaptureFrameAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true);
        if (frame == null) return;
        await RunIncrementalRecognitionCoreAsync(frame);
    }
    [RelayCommand] private async Task DetectStageFromSelectedTestFrameAsync()
    {
        if (SelectedTestFrame == null) return;
        try { await RunPhaseOnlyRecognitionCoreAsync(LoadTestFrame(SelectedTestFrame)); }
        catch (Exception ex) { LastError = ex.Message; }
    }
    [RelayCommand] private async Task DetectStageFromCurrentCaptureFrameAsync()
    {
        var frame = await GetValidatedCurrentFrameAsync(requireOcrReady: true);
        if (frame == null) return;
        await RunPhaseOnlyRecognitionCoreAsync(frame);
    }
    [RelayCommand] private Task RunAutomaticOneTickAsync() => RunAutomaticCurrentFrameCoreAsync();
    /// <summary>
    /// 启动自动识别循环。
    /// </summary>
    /// <returns>启动流程完成后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanStartAutomaticRecognition))]
    private async Task StartPreviewLoopAsync()
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
            await _autoRecognitionCoordinator.StartAsync();
            _isAutomaticRecognitionStopPendingAfterQueueDrain = false;
            _automaticRecognitionUnavailableFrameCount = 0;
            IsPreviewLoopRunning = true;
            _autoRecognitionCoordinator.SampleFrame(frame);
            _frameSamplingTimer.Start();
            _previewTimer.Start();
            _autoRecognitionGlobalControl.Update(true, _ => StopPreviewLoopAsync(), _ => ForceSyncGameStateAsync());
        }
        catch (Exception ex)
        {
            _previewTimer.Stop();
            _frameSamplingTimer.Stop();
            IsPreviewLoopRunning = false;
            _autoRecognitionGlobalControl.Update(false);
            LastError = ex.ToString();
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
    private async Task StopPreviewLoopAsync()
    {
        _isAutomaticRecognitionStopPendingAfterQueueDrain = false;
        _automaticRecognitionUnavailableFrameCount = 0;
        _previewTimer.Stop();
        _frameSamplingTimer.Stop();
        await _autoRecognitionCoordinator.StopAsync();
        IsPreviewLoopRunning = false;
        _autoRecognitionGlobalControl.Update(false);
        NotifyAutomaticRecognitionCommands();
    }

    /// <summary>
    /// 判断当前是否允许启动自动识别循环。
    /// </summary>
    private bool CanStartAutomaticRecognition() => !IsPreviewLoopRunning && !IsRecognizing;

    /// <summary>
    /// 判断当前是否允许停止自动识别循环。
    /// </summary>
    private bool CanStopAutomaticRecognition() => IsPreviewLoopRunning || IsRecognizing;

    /// <summary>
    /// 刷新自动识别启动/停止命令状态。
    /// </summary>
    private void NotifyAutomaticRecognitionCommands()
    {
        StartPreviewLoopCommand.NotifyCanExecuteChanged();
        StopPreviewLoopCommand.NotifyCanExecuteChanged();
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
            if (languages.Length == 0) { LastError = ResolveLocalizedOrRaw("SmartBpTesseractNoLanguageSelected"); return; }
            TesseractLanguages = string.Join('+', languages);
            await _tesseractDataAssetManager.InstallLanguagesAsync(languages);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LastError = ex.ToString(); }
    }

    /// <summary>
    /// 取消 Tesseract traineddata 下载。
    /// </summary>
    [RelayCommand] private void CancelTesseractDataDownload() => _tesseractDataAssetManager.Cancel();

    [RelayCommand] private void PauseTesseractDataDownload() => _tesseractDataAssetManager.Pause();

    [RelayCommand] private void ResumeTesseractDataDownload() => _tesseractDataAssetManager.Resume();

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
            LastError = ex.ToString();
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
            if (languages.Length == 0) { LastError = ResolveLocalizedOrRaw("SmartBpTesseractNoLanguageSelected"); return; }
            await _tesseractDataAssetManager.DeleteAsync(languages);
            await RefreshTesseractDataStatusAsync();
        }
        catch (Exception ex) { LastError = ex.Message; }
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
        catch (Exception ex) { LastError = ex.Message; }
    }

    /// <summary>
    /// 取消 RapidOCR 模型下载。
    /// </summary>
    [RelayCommand]
    private void CancelRapidOcrDownload() => _rapidOcrModelAssetManager.Cancel();

    [RelayCommand]
    private void PauseRapidOcrDownload() => _rapidOcrModelAssetManager.Pause();

    [RelayCommand]
    private void ResumeRapidOcrDownload() => _rapidOcrModelAssetManager.Resume();

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
        catch (Exception ex) { LastError = ex.Message; }
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
            LastError = ex.Message;
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
        IsRecognizing = true; LastError = "";
        try
        {
            var result = await _autoRecognitionCoordinator.RunOneTickAsync(frame);
            ApplyRegionGatedResult(result);
            if (result.SceneGate?.ShouldPauseAutomaticRecognition == true && IsPreviewLoopRunning)
            {
                await RequestStopAutomaticRecognitionAfterQueueDrainedAsync(result);
            }
        }
        catch (OperationCanceledException) { }
        finally { IsRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task StopAutomaticRecognitionForCaptureIssueAsync(string messageKey)
    {
        var message = ResolveLocalizedOrRaw(messageKey);
        _previewTimer.Stop();
        _frameSamplingTimer.Stop();
        await _autoRecognitionCoordinator.StopAsync();
        IsPreviewLoopRunning = false;
        IsRecognizing = false;
        _autoRecognitionGlobalControl.Update(false);
        _automaticRecognitionUnavailableFrameCount = 0;
        LastError = message;
        SceneDiagnostics = string.IsNullOrWhiteSpace(SceneDiagnostics)
            ? message
            : SceneDiagnostics + Environment.NewLine + message;
        _debugLog.Write("recognition", message);
        NotifyAutomaticRecognitionCommands();
        await MessageBoxHelper.ShowInfoAsync(message);
    }

    private async Task RequestStopAutomaticRecognitionAfterQueueDrainedAsync(SmartBpAutoRecognitionTickResult result)
    {
        if (_isAutomaticRecognitionStopPendingAfterQueueDrain)
            return;

        _isAutomaticRecognitionStopPendingAfterQueueDrain = true;
        _previewTimer.Stop();
        _frameSamplingTimer.Stop();
        var phase = result.PhaseResult?.Phase ?? result.BusinessState?.Phase ?? "未知";
        var scene = result.SceneGate?.Scene.ToString() ?? "Unknown";
        var pendingMessage =
            $"Post-BP phase detected: phase={phase}; scene={scene}.{Environment.NewLine}" +
            "Character BP has ended; no new recognition ticks will be scheduled." + Environment.NewLine +
            "Automatic recognition stop is queued after pending operations drain." + Environment.NewLine +
            "Skipped content field recognition because BP ended.";
        SceneDiagnostics = string.IsNullOrWhiteSpace(SceneDiagnostics)
            ? pendingMessage
            : SceneDiagnostics + Environment.NewLine + pendingMessage;
        _debugLog.Write("recognition", pendingMessage);

        await CompleteAutomaticRecognitionAfterQueueDrainedAsync("SmartBpCharacterBpEnded");
    }

    private async Task CompleteAutomaticRecognitionAfterQueueDrainedAsync(string reason)
    {
        await _autoRecognitionCoordinator.CompleteAsync();
        _gameGuidanceService.CompleteGuidance(reason);
        IsPreviewLoopRunning = false;
        _autoRecognitionGlobalControl.Update(false);
        _isAutomaticRecognitionStopPendingAfterQueueDrain = false;
        var completedMessage =
            $"Pending BP operation queue drained.{Environment.NewLine}" +
            $"Automatic recognition stopped.{Environment.NewLine}" +
            $"GameGuidance completed with reason={reason}.";
        SceneDiagnostics = string.IsNullOrWhiteSpace(SceneDiagnostics)
            ? completedMessage
            : SceneDiagnostics + Environment.NewLine + completedMessage;
        _debugLog.Write("recognition", completedMessage);
        LastError = ResolveLocalizedOrRaw("SmartBpRecognitionPausedBpEnded");
        NotifyAutomaticRecognitionCommands();
    }

    private async Task RunRegionGatedFrameCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsRecognizing = true; LastError = "";
        try
        {
            var result = await _autoRecognitionCoordinator.RunOneTickAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunFullStrategyRecognitionCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsRecognizing = true; LastError = "";
        try
        {
            DebugModeSummary = "FullImage";
            var result = await _autoRecognitionCoordinator.RunFullRecognitionDebugAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunPhaseOnlyRecognitionCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsRecognizing = true; LastError = "";
        try
        {
            DebugModeSummary = "PhaseOnly";
            var result = await _autoRecognitionCoordinator.RunPhaseOnlyDebugAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunIncrementalRecognitionCoreAsync(BitmapSource frame)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsRecognizing = true; LastError = "";
        try
        {
            DebugModeSummary = "CurrentStageIncremental";
            var result = await _autoRecognitionCoordinator.RunIncrementalRecognitionDebugAsync(frame);
            ApplyRegionGatedResult(result);
        }
        finally { IsRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private async Task RunOcrSelectedTestFrameCoreAsync(BitmapSource frame, SmartBpRecognitionTask task)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsRecognizing = true; LastError = "";
        try
        {
            var watch = Stopwatch.StartNew();
            var regions = GetOcrContentRegionsForTestFrame(task);
            var result = await _ocrBpRecognitionService.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(regions));
            watch.Stop();

            RawResponse = FormatOcrRawLines(result);
            StageDetectionResult = FormatBusinessState(result.BusinessState);
            GuidanceSnapshot = FormatGuidance(_gameGuidanceService.GetRuntimeSnapshot(), "OCR selected test frame uses direct OCR regions.");
            CandidateOperations = string.Join(Environment.NewLine, result.Diagnostics);
            ParsedVisualResult = StageDetectionResult;
            NormalizedResult = CandidateOperations;
            PhaseCropPreview = null;
            FocusedCropPreview = null;
            CropDebugInfo = $"OCR selected test frame task={task}; regions=[{string.Join(", ", regions.Select(GetRecognitionRegionId))}]";
            SceneDiagnostics = "OCR selected test frame bypasses automatic scene gating.";
            RequestMetrics = $"OCR request elapsed: {watch.ElapsedMilliseconds}ms; regions=[{string.Join(", ", regions.Select(GetRecognitionRegionId))}]";
            LastError = "";
        }
        finally { IsRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private void ApplyRegionGatedResult(SmartBpAutoRecognitionTickResult result)
    {
        WriteStandardBpRecognitionDebugLog(result);
        RefreshStrategyDebugSections(result);
        RawResponse = result.RawJson;
        StageDetectionResult = result.BusinessState == null ? "-" : FormatBusinessState(result.BusinessState);
        GuidanceSnapshot = FormatGuidance(result.GuidanceSnapshot, result.GuidanceSync?.Reason);
        CandidateOperations = FormatAutomaticOperations(result);
        LastSmartBpProgressDiagnosis = FormatProgressDiagnosis(result);
        ParsedVisualResult = StageDetectionResult;
        NormalizedResult = CandidateOperations;
        PhaseCropPreview = result.PhaseCrop?.Image;
        FocusedCropPreview = result.ContentCrops?.LastOrDefault()?.Image ?? result.FocusedCrop?.Image;
        CropDebugInfo = FormatCropDebugInfo(result);
        SceneDiagnostics = result.SceneGate == null ? "-" :
            $"Scene: {result.SceneGate.Scene}{Environment.NewLine}BP recognition allowed: {result.SceneGate.IsBpRecognitionAllowed}{Environment.NewLine}Character operations allowed: {result.SceneGate.IsCharacterOperationAllowed}{Environment.NewLine}Action: {(result.SceneGate.ShouldPauseAutomaticRecognition ? "automatic recognition paused" : "continue monitoring")}{Environment.NewLine}Reason: {result.SceneGate.Reason}";
        RequestMetrics = FormatRecognitionTiming();
        LastError = result.Error ?? "";
        RefreshRecognitionDebugLogText();
    }

    /// <summary>
    /// 将标准 BP 对局流本次识别的原始响应、诊断和候选结果写入统一调试日志窗口。
    /// </summary>
    /// <param name="result">本次自动或手动识别结果。</param>
    private void WriteStandardBpRecognitionDebugLog(SmartBpAutoRecognitionTickResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"tick result: phase=[{result.PhaseResult?.Phase ?? "unknown"}]; " +
            $"scene=[{result.SceneGate?.Scene.ToString() ?? "unknown"}]; " +
            $"operations={result.Operations.Count}; " +
            $"applied={result.ApplyResult?.AppliedCount.ToString() ?? "-"}; " +
            $"skipped={result.ApplyResult?.SkippedCount.ToString() ?? "-"}; " +
            $"error=[{result.Error ?? string.Empty}].");

        if (result.CandidateMessages.Count > 0)
        {
            builder.AppendLine("candidate_messages:");
            foreach (var message in result.CandidateMessages)
                builder.AppendLine($"  {message}");
        }

        if (!string.IsNullOrWhiteSpace(result.RawJson))
        {
            builder.AppendLine("raw_response:");
            builder.AppendLine(result.RawJson);
        }

        if (result.RegionSnapshot?.RawResponses is { Count: > 0 } rawResponses)
        {
            builder.AppendLine("region_raw_responses:");
            foreach (var response in rawResponses)
            {
                builder.AppendLine($"  [{response.Key}]");
                builder.AppendLine(response.Value);
            }
        }

        _debugLog.Write("standard-bp", builder.ToString().TrimEnd());
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

    private string FormatGameStateSyncResult(SmartBpGameStateSyncResult result)
    {
        var progressMessage = FormatProgressSyncResult(result.ProgressSync);
        if (result.ApplyResult == null)
            return progressMessage;

        return string.Format(
            ResolveLocalizedOrRaw("SmartBpGameStateSyncAppliedFormat"),
            result.ApplyResult.AppliedCount,
            result.ApplyResult.SkippedCount,
            progressMessage);
    }

    private void ShowForceSyncGameStateInfoBar(SmartBpGameStateSyncResult result)
    {
        var message = FormatGameStateSyncResult(result);
        if (!result.ProgressSync.Succeeded)
        {
            _infoBarService.ShowWarningInfoBar(message);
            return;
        }

        if (result.ApplyResult?.SkippedCount > 0)
            _infoBarService.ShowWarningInfoBar(message);
        else
            _infoBarService.ShowSuccessInfoBar(message);
    }

    private string FormatProgressDiagnosis(SmartBpAutoRecognitionTickResult result)
    {
        if (result.ProgressSync?.Moved == true)
            return string.Format(
                ResolveLocalizedOrRaw("SmartBpProgressDiagnosisSyncedFormat"),
                result.ProgressSync.TargetStepIndex,
                result.ProgressSync.TargetAction,
                string.Join(",", result.ProgressSync.TargetIndexes));
        if (result.ProgressSync?.Succeeded == true)
            return ResolveLocalizedOrRaw("SmartBpProgressDiagnosisAligned");
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
        DebugPhaseScene = "-";
        DebugOcrRawLines = "-";
        DebugParsedState = "-";
        DebugMergeLog = "-";
        DebugCandidateOperations = "-";
        DebugTiming = "-";
        RefreshRecognitionDebugLogText();
    }

    private void RefreshStrategyDebugSections(SmartBpAutoRecognitionTickResult result)
    {
        DebugStrategySummary = $"debug_mode={DebugModeSummary}{Environment.NewLine}ocr_provider={_recognitionSettingsService.Settings.SelectedOcrProviderMode}";
        DebugPhaseScene = result.SceneGate == null
            ? $"phase={result.PhaseResult?.Phase ?? "unknown"}"
            : $"phase={result.PhaseResult?.Phase ?? "unknown"}{Environment.NewLine}scene={result.SceneGate.Scene}{Environment.NewLine}bp_allowed={result.SceneGate.IsBpRecognitionAllowed}{Environment.NewLine}character_operations_allowed={result.SceneGate.IsCharacterOperationAllowed}{Environment.NewLine}reason={result.SceneGate.Reason}";
        DebugParsedState = result.BusinessState == null ? "-" : FormatBusinessState(result.BusinessState);
        DebugFinalBusinessState = result.BusinessState == null
                ? "Recognition failed before final business state was produced."
                : DebugParsedState;
        DebugCandidateOperations = FormatAutomaticOperations(result);
        DebugTiming = FormatRecognitionTiming();
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

        DebugOcrRawLines = ExtractOcrRaw(result.RawJson);
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
{DebugLogText}

=== Last Error ===
{LastError}
""";
    }

    partial void OnDebugLogTextChanged(string value) => RefreshRecognitionDebugLogText();

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

    private void RefreshGuidanceSnapshot() => GuidanceSnapshot = FormatGuidance(_gameGuidanceService.GetRuntimeSnapshot());
    private async Task RecognizeCoreAsync(BitmapSource frame, SmartBpRecognitionTask task)
        => await RunOcrSelectedTestFrameCoreAsync(frame, task);

    [ObservableProperty]
    public partial string ParsedVisualResult { get; set; } = "";

    // 本模块仅提供 OCR 识别；下列本地视觉模型切换实现已停止注册。
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

    partial void OnIsRecognizingChanged(bool value)
    {
        StopCaptureCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPreviewLoopRunningChanged(bool value)
    {
        StopCaptureCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseMultiImageSnapshotRequestChanged(bool value)
    {
        _recognitionSettingsService.Settings.UseMultiImageSnapshotRequest = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionVisualBufferMillisecondsChanged(int value)
    {
        _recognitionSettingsService.Settings.RecognitionVisualBufferMilliseconds = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
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

    partial void OnOcrBackfillLookBehindStepsChanged(int value)
    {
        _recognitionSettingsService.Settings.OcrBackfillLookBehindSteps = Math.Clamp(value, 0, 20);
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionTransitionLookBehindMillisecondsChanged(int value)
    {
        _recognitionSettingsService.Settings.RecognitionTransitionLookBehindMilliseconds = Math.Clamp(value, 100, 5000);
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionTransitionReplayMinimumConfidenceChanged(double value)
    {
        _recognitionSettingsService.Settings.RecognitionTransitionReplayMinimumConfidence = Math.Clamp(value, 0, 1);
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnRecognitionIntervalMsChanged(int value)
    {
        var minimum = Math.Max(100, _recognitionSettingsService.Settings.MinimumRecognitionIntervalMs);
        _recognitionSettingsService.Settings.RecognitionIntervalMs = Math.Clamp(value, minimum, 300000);
        RefreshRecognitionTimerInterval();
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnUseOcrContactSheetChanged(bool value)
    {
        _recognitionSettingsService.Settings.UseOcrContactSheet = value;
        RefreshRecognitionSpeedTestValidity();
        _ = _recognitionSettingsService.SaveAsync();
    }

    // OCR-only 模式不再保存已废弃的融合策略。
    partial void OnEnableOcrDebugOverlayChanged(bool value)
    {
        _recognitionSettingsService.Settings.EnableOcrDebugOverlay = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnSelectedOcrProviderChanged(OcrProviderSelection? value)
    {
        if (value == null) return;
        // 语言切换触发 RebuildOcrProviderSelections 重建列表时，SelectedOcrProvider 会被
        // 重新赋值以恢复用户之前的选择；此时不应写回设置或触发状态级联，避免误持久化与重复刷新。
        if (_isRebuildingProviders) return;
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

    /// <summary>
    /// 重建 OCR Provider 选择列表并恢复选中项。
    /// 列表中的显示名（如 "PaddleOCR（推荐）"）通过资源键本地化，语言切换后必须重新计算。
    /// 重建期间通过 <see cref="_isRebuildingProviders"/> 抑制 <see cref="OnSelectedOcrProviderChanged"/>
    /// 的设置写回，避免语言切换误触发持久化与状态级联。
    /// </summary>
    private void RebuildOcrProviderSelections()
    {
        var previouslySelectedMode = SelectedOcrProvider?.Mode
            ?? _recognitionSettingsService.Settings.SelectedOcrProviderMode;

        _isRebuildingProviders = true;
        try
        {
            OcrProviders =
            [
                new(SmartBpOcrProviderMode.Paddle, string.Format(ResolveLocalizedOrRaw("SmartBpRecommendedProviderFormat"), "PaddleOCR")),
                new(SmartBpOcrProviderMode.Rapid, "RapidOCR"),
                new(SmartBpOcrProviderMode.Tesseract, "Tesseract OCR")
            ];
            SelectedOcrProvider = OcrProviders.FirstOrDefault(item => item.Mode == previouslySelectedMode)
                                  ?? OcrProviders.First();
        }
        finally
        {
            _isRebuildingProviders = false;
        }
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

    // 本模块仅提供 OCR 识别；下列本地视觉模型下载状态映射已停止注册。
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

    /// <summary>一个可选择的 OCR Provider选项。</summary>
    /// <param name="Mode">持久化使用的Provider模式。</param>
    /// <param name="DisplayName">界面显示名称。</param>
    public sealed record OcrProviderSelection(SmartBpOcrProviderMode Mode, string DisplayName);

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
