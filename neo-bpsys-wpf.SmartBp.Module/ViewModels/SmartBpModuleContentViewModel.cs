using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Windows;
using Windows.Graphics.Capture;
using WPFLocalizeExtension.Engine;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// SmartBP 页面视图模型，管理窗口捕获、OCR 模型下载/切换/删除、识别区域配置等 SmartBP 核心功能。
/// </summary>
public partial class SmartBpModuleContentViewModel : ViewModelBase
{
    // WGC API 可用最低版本：Windows 10 1803。
    private const int WgcMinimumBuild = 17134; // Windows 10 1803

    // HWND 直捕（CreateForWindow）可用最低版本：Windows 10 1903。
    private const int WgcHwndInteropMinimumBuild = 18362; // Windows 10 1903

    private readonly IWindowCaptureService _windowCaptureService = null!;
    private readonly IOcrService _ocrService = null!;
    private readonly ILogger<SmartBpModuleContentViewModel> _logger;
    private readonly ISmartBpRecognitionSettingsService _recognitionSettingsService = null!;
    private readonly ISmartBpDebugLog _debugLog = null!;
    private readonly ISmartBpAutoRecognitionCoordinator _autoRecognitionCoordinator = null!;
    private readonly IGameGuidanceService _gameGuidanceService = null!;
    private readonly ISmartBpCharacterResolver _smartBpCharacterResolver = null!;
    private readonly ISmartBpRecognitionRegionProfileService _aiRegionProfileService = null!;
    private readonly ISmartBpRecognitionLedger _recognitionLedger = null!;
    private readonly ISmartBpRecognitionStateStore _recognitionStateStore = null!;
    private readonly ISmartBpGameStateSyncService _gameStateSyncService = null!;
    private readonly IInfoBarService _infoBarService = null!;
    private readonly ITesseractDataAssetManager _tesseractDataAssetManager = null!;
    private readonly IRapidOcrModelAssetManager _rapidOcrModelAssetManager = null!;
    private readonly ISmartBpAutoRecognitionGlobalControlSink _autoRecognitionGlobalControl = null!;
    private readonly ISmartBpOcrBpRecognitionService _ocrBpRecognitionService = null!;
    private readonly ISmartBpModuleStorageProvider _smartBpModuleStorage = null!;
    private readonly IGameDataRecognitionDebugState _gameDataRecognitionDebugState = null!;
    private readonly object _debugLogBufferLock = new();
    private readonly StringBuilder _debugLogBuffer = new();
    private DispatcherTimer? _debugLogFlushTimer;
    private Window? _recognitionDebugLogWindow;
    private const string CaptureNotRunningMessageKey = "SmartBpValidationCaptureNotRunning";
    private const string CaptureFrameUnavailableMessageKey = "SmartBpValidationCaptureFrameUnavailable";
    private const string OcrNotReadyMessageKey = "SmartBpValidationOcrNotReady";

    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
#pragma warning disable CS8618
    public SmartBpModuleContentViewModel()
#pragma warning restore CS8618
    {
        // 仅供设计器构造预览使用。
    }

    /// <summary>
    /// SmartBp 页面视图模型构造函数。
    /// </summary>
    public SmartBpModuleContentViewModel(
        IWindowCaptureService windowCaptureService,
        IOcrService ocrService,
        ISmartBpRecognitionSettingsService recognitionSettingsService,
        ISmartBpDebugLog aiDebugLog,
        ISmartBpAutoRecognitionCoordinator autoRecognitionCoordinator,
        IGameGuidanceService gameGuidanceService,
        ISmartBpCharacterResolver smartBpCharacterResolver,
        ISmartBpRecognitionRegionProfileService aiRegionProfileService,
        ISmartBpRecognitionLedger aiRecognitionLedger,
        ISmartBpRecognitionStateStore aiRecognitionStateStore,
        ISmartBpGameStateSyncService gameStateSyncService,
        IInfoBarService infoBarService,
        ITesseractDataAssetManager tesseractDataAssetManager,
        IRapidOcrModelAssetManager rapidOcrModelAssetManager,
        ISmartBpAutoRecognitionGlobalControlSink autoRecognitionGlobalControl,
        ISmartBpOcrBpRecognitionService ocrBpRecognitionService,
        ISmartBpModuleStorageProvider smartBpModuleStorage,
        IGameDataRecognitionDebugState gameDataRecognitionDebugState,
        ILogger<SmartBpModuleContentViewModel> logger)
    {
        _logger = logger;
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _recognitionSettingsService = recognitionSettingsService;
        _debugLog = aiDebugLog;
        _autoRecognitionCoordinator = autoRecognitionCoordinator;
        _gameGuidanceService = gameGuidanceService;
        _smartBpCharacterResolver = smartBpCharacterResolver;
        _aiRegionProfileService = aiRegionProfileService;
        _recognitionLedger = aiRecognitionLedger;
        _recognitionStateStore = aiRecognitionStateStore;
        _gameStateSyncService = gameStateSyncService;
        _infoBarService = infoBarService;
        _tesseractDataAssetManager = tesseractDataAssetManager;
        _rapidOcrModelAssetManager = rapidOcrModelAssetManager;
        _autoRecognitionGlobalControl = autoRecognitionGlobalControl;
        _ocrBpRecognitionService = ocrBpRecognitionService;
        _smartBpModuleStorage = smartBpModuleStorage;
        _gameDataRecognitionDebugState = gameDataRecognitionDebugState;
        _gameDataRecognitionDebugState.SnapshotChanged += (_, _) => BeginOnUiThread(RefreshGameDataRecognitionDebugText);
        RefreshGameDataRecognitionDebugText();
        InitializeRecognition();
        _ocrService.DownloadStateChanged += OcrService_DownloadStateChanged;
        _ocrService.ModelLoadStateChanged += OcrService_ModelLoadStateChanged;
        // 配置被保存/导入/重置时同步刷新比例状态展示。

        ActiveWindows = _windowCaptureService.ListActiveWindows();

        if (!IsWgcHwndCaptureSupported())
        {
            SelectedCaptureMethod = CaptureMethod.Bitblt;
            SelectedCaptureMethodIndex = 1;
        }

        RefreshOcrModelStatus();
        SyncDownloadStateFromService();
        IsOcrModelLoading = _ocrService.IsModelLoading;

        // 在 UI 空闲后触发后台模型加载，避免与 View 渲染竞争 loader lock。
        Application.Current?.Dispatcher?.BeginInvoke(
            new Action(() => _ocrService.StartLoadingPreferredModel()),
            DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// 最近一次赛后数据整表 OCR 重建出的表格行。
    /// </summary>
    public ObservableCollection<GameDataRecognitionDebugRow> GameDataRecognitionDebugRows { get; } = [];

    /// <summary>
    /// 最近一次赛后 OCR 返回的文本行数量。
    /// </summary>
    [ObservableProperty]
    public partial int GameDataRecognitionOcrLineCount { get; set; }

    /// <summary>
    /// 最近一次赛后 OCR 文本行数量的本地化展示文本。
    /// </summary>
    [ObservableProperty]
    public partial string GameDataRecognitionOcrLineCountText { get; set; } = "-";

    /// <summary>
    /// 最近一次赛后整表 OCR 的坐标解析诊断文本。
    /// </summary>
    [ObservableProperty]
    public partial string GameDataRecognitionDiagnosticsText { get; set; } = "-";

    /// <summary>
    /// 将最近一次整表 OCR 快照同步到页面调试表格。
    /// </summary>
    private void RefreshGameDataRecognitionDebugText()
    {
        var snapshot = _gameDataRecognitionDebugState.Current;
        GameDataRecognitionOcrLineCount = snapshot.OcrLineCount;
        GameDataRecognitionOcrLineCountText = string.Format(
            ResolveLocalizedOrRaw("SmartBpGameDataDebugOcrLineCountFormat"),
            snapshot.OcrLineCount);
        GameDataRecognitionDebugRows.Clear();
        foreach (var row in snapshot.Rows.OrderBy(row => row.RowIndex))
            GameDataRecognitionDebugRows.Add(row with
            {
                ResolvedCharacterName = row.ResolvedCharacterName ?? ResolveLocalizedOrRaw("SmartBpGameDataDebugUnresolved")
            });

        GameDataRecognitionDiagnosticsText = snapshot.Diagnostics.Count == 0
            ? "-"
            : string.Join(Environment.NewLine, snapshot.Diagnostics);
    }

    /// <summary>
    /// 当前活动窗口列表。
    /// </summary>
    [ObservableProperty]
    public partial List<WindowInfo> ActiveWindows { get; set; } = [];

    /// <summary>
    /// 可选 OCR 模型列表。
    /// </summary>
    [ObservableProperty]
    public partial List<OcrModelSelection> OcrModelList { get; set; } = [];

    /// <summary>
    /// 当前选中的 OCR 模型。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedOcrModelCommand))]
    public partial OcrModelSelection? SelectedOcrModel { get; set; }

    /// <summary>
    /// 是否正在下载 OCR 模型。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedOcrModelCommand))]
    public partial bool IsModelDownloading { get; set; }

    /// <summary>
    /// OCR 首选模型是否正在后台加载。
    /// </summary>
    [ObservableProperty]
    public partial bool IsOcrModelLoading { get; set; }

    /// <summary>
    /// 是否有精确下载进度（区别于不确定进度条）。
    /// </summary>
    [ObservableProperty]
    public partial bool HasPreciseDownloadProgress { get; set; }

    /// <summary>
    /// OCR 模型下载进度值（0-100）。
    /// </summary>
    [ObservableProperty]
    public partial double ModelDownloadProgress { get; set; }

    /// <summary>
    /// OCR 模型下载进度文本（百分比）。
    /// </summary>
    [ObservableProperty]
    public partial string ModelDownloadProgressText { get; set; } = string.Empty;

    /// <summary>
    /// OCR 模型下载阶段描述文本。
    /// </summary>
    [ObservableProperty]
    public partial string ModelDownloadStageText { get; set; } = string.Empty;

    /// <summary>
    /// 当前 OCR 模型显示名称。
    /// </summary>
    [ObservableProperty]
    public partial string CurrentOcrModelDisplayName { get; set; } = "";

    /// <summary>
    /// 是否显示下载模型按钮。
    /// </summary>
    public bool ShowDownloadModelButton => SelectedOcrModel is not { IsInstalled: true };

    /// <summary>
    /// 是否显示删除模型按钮。
    /// </summary>
    public bool ShowDeleteModelButton => SelectedOcrModel is { IsInstalled: true };

    private WindowInfo? _selectedWindow;

    /// <summary>
    /// 当前选中的捕获窗口。
    /// </summary>
    public WindowInfo? SelectedWindow
    {
        get => _selectedWindow;
        set => SetPropertyWithAction(ref _selectedWindow, value, _ =>
        {
            StartCaptureCommand.NotifyCanExecuteChanged();
            if (_windowCaptureService.IsCapturing)
                StartCapture();
        });
    }

    /// <summary>
    /// 刷新可捕获窗口列表。
    /// </summary>
    [RelayCommand]
    private void RefreshActiveWindows() => ActiveWindows = _windowCaptureService.ListActiveWindows();

    /// <summary>
    /// 按当前选择的窗口与捕获方式启动窗口捕获。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCaptureStarted))]
    private void StartCapture()
    {
        _ = _windowCaptureService.StartCapture(SelectedWindow, SelectedCaptureMethod);
        // 捕获状态变化会影响多个按钮的可用性。
        RefreshCommandStates();
    }

    /// <summary>
    /// 停止当前窗口捕获，并停止捕获比例刷新计时器。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCaptureStopped))]
    private void StopCapture()
    {
        if (IsPreviewLoopRunning || IsRecognizing)
        {
            _ = MessageBoxHelper.ShowInfoAsync(ResolveLocalizedOrRaw("SmartBpStopCaptureWhileRecognizing"));
            return;
        }

        _windowCaptureService.StopCapture();
        RefreshCommandStates();
    }

    /// <summary>
    /// 打开当前捕获源的预览窗口。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenPreviewWindow))]
    private void OpenPreviewWindow() => _windowCaptureService.OpenPreviewWindow();

    /// <summary>
    /// 打开 Windows Graphics Capture 系统窗口选择器。
    /// </summary>
    /// <returns>窗口选择流程完成后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanOpenWindowPicker))]
    private async Task OpenWindowPickerAsync()
    {
        if (await _windowCaptureService.StartCaptureWithPickerAsync())
        {
            SelectedCaptureMethod = CaptureMethod.WGC;
        }

        RefreshCommandStates();
    }

    [RelayCommand]
    /// <summary>
    /// 刷新 PaddleOCR 模型列表、安装状态和当前模型显示名。
    /// </summary>
    private void RefreshOcrModelStatus()
    {
        var preferredSelectedKey = SelectedOcrModel?.Key;
        var currentModelKey = _ocrService.CurrentOcrModelKey;
        var recommendedModelKey = GetRecommendedModelKeyForCurrentLanguage();
        OcrModelList =
        [
            .. _ocrService.GetAvailableModels()
                .OrderByDescending(m => m.Key == recommendedModelKey)
                .Select(m => new OcrModelSelection(
                    m.Key,
                    ResolveLocalizedOrRaw(m.DisplayName),
                    ResolveLocalizedOrRaw(m.Description),
                    _ocrService.IsModelInstalled(m.Key),
                    m.Key == currentModelKey))
        ];

        SelectedOcrModel = OcrModelList.FirstOrDefault(m => m.Key == preferredSelectedKey)
                           ?? OcrModelList.FirstOrDefault(m => m.Key == currentModelKey)
                           ?? OcrModelList.FirstOrDefault(m => m.Key == recommendedModelKey)
                           ?? OcrModelList.FirstOrDefault();

        CurrentOcrModelDisplayName = currentModelKey is null
            ? ResolveLocalizedOrRaw("SmartBpCurrentOcrModelDisabled")
            : string.Format(
                ResolveLocalizedOrRaw("SmartBpCurrentOcrModelFormat"),
                OcrModelList.FirstOrDefault(m => m.Key == currentModelKey)?.DisplayName ?? currentModelKey);
        RefreshOcrProviderStatuses();
    }

    /// <summary>
    /// 下载当前选择的 PaddleOCR 模型。
    /// </summary>
    /// <returns>下载流程完成后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanDownloadSelectedOcrModel))]
    private async Task DownloadSelectedOcrModelAsync()
    {
        if (SelectedOcrModel == null)
            return;

        try
        {
            await _ocrService.DownloadModelAsync(SelectedOcrModel.Key);
            RefreshOcrModelStatus();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await MessageBoxHelper.ShowErrorAsync(
                string.Format(
                    I18nHelper.GetLocalizedString("SmartBpOcrModelDownloadFailed"),
                    ex.Message));
        }
    }

    /// <summary>
    /// 删除当前选择的 PaddleOCR 模型。
    /// </summary>
    /// <returns>删除流程完成后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanDeleteSelectedOcrModel))]
    private async Task DeleteSelectedOcrModelAsync()
    {
        if (SelectedOcrModel == null)
            return;

        var confirmed = await MessageBoxHelper.ShowConfirmAsync(
            string.Format(I18nHelper.GetLocalizedString("SmartBpDeleteOcrModelConfirmFormat"),
                ResolveLocalizedOrRaw(SelectedOcrModel.DisplayName)),
            I18nHelper.GetLocalizedString("SmartBpDeleteOcrModelTitle"),
            I18nHelper.GetLocalizedString("Delete"),
            I18nHelper.GetLocalizedString("Cancel"));
        if (!confirmed)
            return;

        if (!_ocrService.TryDeleteModel(SelectedOcrModel.Key, out var errorMessage))
        {
            await MessageBoxHelper.ShowErrorAsync(errorMessage);
            return;
        }

        RefreshOcrModelStatus();
    }

    /// <summary>
    /// 取消正在进行的 PaddleOCR 模型下载。
    /// </summary>
    [RelayCommand]
    private void CancelOcrModelDownload()
    {
        _ocrService.CancelDownload();
    }

    /// <summary>
    /// 切换当前 PaddleOCR 模型。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSwitchSelectedOcrModel))]
    private void SwitchSelectedOcrModel()
    {
        if (SelectedOcrModel == null)
            return;

        if (!_ocrService.TrySwitchOcrModel(SelectedOcrModel.Key, out var errorMessage))
        {
            _ = MessageBoxHelper.ShowErrorAsync(errorMessage);
            return;
        }

        RefreshOcrModelStatus();
    }

    /// <summary>
    /// 判断当前是否允许启动窗口捕获。
    /// </summary>
    /// <returns>允许启动返回 <see langword="true"/>。</returns>
    private bool CanCaptureStarted() =>
        SelectedCaptureMethod == CaptureMethod.WGC
            ? SelectedWindow is not null && IsWgcHwndCaptureSupported()
            : SelectedWindow is not null;

    private bool CanCaptureStopped() =>
        _windowCaptureService is { IsCapturing: true } && !IsPreviewLoopRunning && !IsRecognizing;

    private bool CanOpenPreviewWindow() => _windowCaptureService is { IsCapturing: true };

    private static bool CanOpenWindowPicker() => IsWgcSupported();

    private bool CanDownloadSelectedOcrModel() =>
        !IsModelDownloading && SelectedOcrModel is { IsInstalled: false };

    private bool CanDeleteSelectedOcrModel() =>
        !IsModelDownloading && SelectedOcrModel is { IsInstalled: true };

    private bool CanSwitchSelectedOcrModel() =>
        !IsModelDownloading && SelectedOcrModel is { IsInstalled: true, IsCurrent: false };

    /// <summary>
    /// 刷新捕获、预览、区域编辑和模型管理命令的可执行状态。
    /// </summary>
    private void RefreshCommandStates()
    {
        // 捕获状态变化后，统一刷新和捕获相关的命令可用性。
        StopCaptureCommand.NotifyCanExecuteChanged();
        OpenPreviewWindowCommand.NotifyCanExecuteChanged();
        StartCaptureCommand.NotifyCanExecuteChanged();
        OpenWindowPickerCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 验证捕获会话是否正在运行，并返回当前冻结的帧。
    /// </summary>
    /// <param name="requireOcrReady">所选择的 OCR 提供程序是否必须就绪。</param>
    /// <returns>当前捕获帧；验证失败时返回 <see langword="null"/>。</returns>
    private async Task<System.Windows.Media.Imaging.BitmapSource?> GetValidatedCurrentFrameAsync(bool requireOcrReady, bool useInfoBar = false)
    {
        if (!_windowCaptureService.IsCapturing)
        {
            await ShowValidationMessageAsync(ResolveLocalizedOrRaw(CaptureNotRunningMessageKey), useInfoBar);
            return null;
        }

        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
        {
            await ShowValidationMessageAsync(ResolveLocalizedOrRaw(CaptureFrameUnavailableMessageKey), useInfoBar);
            return null;
        }

        if (requireOcrReady && !IsSelectedOcrProviderReady())
        {
            await ShowValidationMessageAsync(ResolveLocalizedOrRaw(OcrNotReadyMessageKey), useInfoBar);
            return null;
        }

        return frame;
    }

    private static Task ShowMessageBoxValidationMessageAsync(string message) => MessageBoxHelper.ShowInfoAsync(message);

    private Task ShowValidationMessageAsync(string message, bool useInfoBar)
    {
        if (useInfoBar)
        {
            if (_infoBarService is not null)
            {
                _infoBarService.ShowWarningInfoBar(message);
                return Task.CompletedTask;
            }

            // 设计时构造函数不会注入 InfoBarService，保留原提示路径作为兜底。
            return ShowMessageBoxValidationMessageAsync(message);
        }

        return ShowMessageBoxValidationMessageAsync(message);
    }

    /// <summary>
    /// 检查当前所选 OCR 提供程序是否可以执行识别。
    /// </summary>
    /// <returns>OCR 就绪时返回 <see langword="true"/>。</returns>
    private bool IsSelectedOcrProviderReady() => _ocrService.GetProviderStatus(_ocrService.SelectedProvider).IsReady;

    /// <summary>
    /// 处理 OCR 下载状态变化，并异步切回 UI 线程同步绑定属性。
    /// </summary>
    private void OcrService_DownloadStateChanged(object? sender, EventArgs e)
    {
        BeginOnUiThread(SyncDownloadStateFromService);
    }

    private void OcrService_ModelLoadStateChanged(object? sender, EventArgs e)
    {
        BeginOnUiThread(() =>
        {
            IsOcrModelLoading = _ocrService.IsModelLoading;
            if (!IsOcrModelLoading)
                RefreshOcrModelStatus();
        });
    }

    /// <summary>
    /// 从 OCR 服务同步下载进度、阶段文本和命令状态。
    /// </summary>
    private void SyncDownloadStateFromService()
    {
        IsModelDownloading = _ocrService.IsDownloading;
        ModelDownloadStageText = _ocrService.DownloadStatusText;

        if (_ocrService.DownloadProgress is double progress)
        {
            HasPreciseDownloadProgress = true;
            ModelDownloadProgress = progress;
            ModelDownloadProgressText = $"{progress:0.00}%";
        }
        else
        {
            HasPreciseDownloadProgress = false;
            ModelDownloadProgress = 0;
            ModelDownloadProgressText = string.Empty;
        }
    }

    /// <summary>
    /// 在 WPF UI 线程执行绑定属性更新。
    /// </summary>
    /// <param name="action">要执行的 UI 更新。</param>
    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Application.Current.Dispatcher.Invoke(action);
    }

    /// <summary>
    /// 异步在 WPF UI 线程执行操作，不阻塞调用线程。
    /// 用于从非 UI 线程触发的事件处理器，避免 <see cref="Dispatcher.Invoke"/> 同步阻塞
    /// 导致与 Windows loader lock 死锁。
    /// </summary>
    /// <param name="action">要执行的 UI 更新。</param>
    private static void BeginOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(action);
    }

    partial void OnSelectedOcrModelChanged(OcrModelSelection? value)
    {
        OnPropertyChanged(nameof(ShowDownloadModelButton));
        OnPropertyChanged(nameof(ShowDeleteModelButton));
    }

    /// <summary>
    /// 判断当前系统是否支持 Windows Graphics Capture 基础 API。
    /// </summary>
    private static bool IsWgcApiAvailable() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, WgcMinimumBuild);

    /// <summary>
    /// 判断当前系统是否支持基于 HWND 的 WGC 直捕。
    /// </summary>
    private static bool IsWgcHwndInteropAvailable() =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, WgcHwndInteropMinimumBuild);

    /// <summary>
    /// 判断系统与 API contract 是否支持 WGC 窗口选择。
    /// </summary>
    /// <returns>支持 WGC 返回 <see langword="true"/>。</returns>
    private static bool IsWgcSupported()
    {
        if (!IsWgcApiAvailable())
            return false;

        return GraphicsCaptureSession.IsSupported();
    }

    /// <summary>
    /// 判断是否可以使用 HWND 直捕路径。
    /// </summary>
    private static bool IsWgcHwndCaptureSupported() => IsWgcHwndInteropAvailable() && IsWgcSupported();

    /// <summary>
    /// 根据当前应用语言选择推荐 PaddleOCR 模型。
    /// </summary>
    /// <returns>推荐模型 key。</returns>
    private static string GetRecommendedModelKeyForCurrentLanguage()
    {
        var language = LocalizeDictionary.CurrentCulture.Name;
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return "en-v4-mobile";

        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return "ja-v4-mobile";

        return "zh-cn-v5-mobile";
    }

    /// <summary>
    /// 将资源 key 解析为本地化文本；找不到资源时保留原始文本。
    /// </summary>
    /// <param name="keyOrRawText">资源 key 或原始文本。</param>
    /// <returns>本地化文本或原始文本。</returns>
    private static string ResolveLocalizedOrRaw(string keyOrRawText)
    {
        var localized = I18nHelper.GetLocalizedString(keyOrRawText);
        return string.IsNullOrWhiteSpace(localized) ? keyOrRawText : localized;
    }


    /// <summary>
    /// 当前选中的捕获方式。
    /// </summary>
    public CaptureMethod SelectedCaptureMethod { get; set; } = CaptureMethod.WGC;

    /// <summary>
    /// 捕获方式下拉框选中索引。
    /// </summary>
    public int SelectedCaptureMethodIndex { get; set; }

    /// <summary>
    /// 可选捕获方式列表。
    /// </summary>
    public List<CaptureMethodSelection> CaptureMethodList { get; } =
    [
        new(CaptureMethod.WGC, "SmartBpCaptureMethodWgc"),
        new(CaptureMethod.Bitblt, "SmartBpCaptureMethodBitblt")
    ];

    /// <summary>
    /// 捕获方式下拉项模型。
    /// </summary>
    public class CaptureMethodSelection
    {
        /// <summary>
        /// 构造一个捕获方式展示项。
        /// </summary>
        public CaptureMethodSelection(CaptureMethod method, string displayNameKey)
        {
            Method = method;
            DisplayNameKey = displayNameKey;

            if (method == CaptureMethod.WGC && !IsWgcHwndCaptureSupported())
            {
                IsAvaliable = false;
            }
        }

        /// <summary>
        /// 捕获方式值。
        /// </summary>
        public CaptureMethod Method { get; init; }

        /// <summary>
        /// 展示文案的本地化 Key。
        /// </summary>
        public string DisplayNameKey { get; init; }

        /// <summary>
        /// 该选项是否可用。
        /// </summary>
        public bool IsAvaliable { get; init; } = true;
    }

    /// <summary>
    /// OCR 模型下拉项展示模型。
    /// </summary>
    public sealed record OcrModelSelection(
        string Key,
        string DisplayName,
        string Description,
        bool IsInstalled,
        bool IsCurrent);
}
