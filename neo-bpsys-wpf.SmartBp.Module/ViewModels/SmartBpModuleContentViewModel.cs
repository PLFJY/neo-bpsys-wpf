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
    private readonly ISmartBpRegionConfigService _regionConfigService = null!;
    private readonly ISmartBpSceneDefinition _gameDataSceneDefinition = null!;
    private readonly IFilePickerService _filePickerService = null!;
    private readonly DispatcherTimer _captureAspectRefreshTimer;
    private readonly ILogger<SmartBpModuleContentViewModel> _logger;
    private readonly IQwenModelAssetManager _qwenAssetManager = null!;
    private readonly ILlamaCppServerManager _llamaServerManager = null!;
    private readonly ILlamaCppServerManagerFactory _llamaServerManagers = null!;
    private readonly ISmartBpRecognitionSettingsService _recognitionSettingsService = null!;
    private readonly ISmartBpDebugLog _aiDebugLog = null!;
    private readonly ISmartBpPromptProfileProvider _promptProfileProvider = null!;
    private readonly ILlamaCppRuntimeAssetManager _llamaRuntimeAssetManager = null!;
    private readonly ISmartBpAutoRecognitionCoordinator _autoRecognitionCoordinator = null!;
    private readonly IGameGuidanceService _gameGuidanceService = null!;
    private readonly ISmartBpCharacterResolver _smartBpCharacterResolver = null!;
    private readonly ISmartBpRecognitionRegionProfileService _aiRegionProfileService = null!;
    private readonly ISmartBpRecognitionLedger _aiRecognitionLedger = null!;
    private readonly ISmartBpRecognitionStateStore _aiRecognitionStateStore = null!;
    private readonly ILlamaCppRuntimeUpdateService _llamaRuntimeUpdateService = null!;
    private readonly ITesseractDataAssetManager _tesseractDataAssetManager = null!;
    private readonly IRapidOcrModelAssetManager _rapidOcrModelAssetManager = null!;
    private readonly ISmartBpAutoRecognitionGlobalControlSink _autoRecognitionGlobalControl = null!;
    private readonly ISmartBpOcrBpRecognitionService _ocrBpRecognitionService = null!;
    private readonly ISmartBpAiPerformanceMonitor _aiPerformanceMonitor = null!;
    private readonly ISmartBpModuleStorageProvider _smartBpModuleStorage = null!;
    private readonly object _debugLogBufferLock = new();
    private readonly StringBuilder _debugLogBuffer = new();
    private DispatcherTimer? _debugLogFlushTimer;
    private Window? _recognitionDebugLogWindow;

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
        ISmartBpRegionConfigService regionConfigService,
        IEnumerable<ISmartBpSceneDefinition> sceneDefinitions,
        IFilePickerService filePickerService,
        IQwenModelAssetManager qwenAssetManager,
        ILlamaCppServerManager llamaServerManager,
        ILlamaCppServerManagerFactory llamaServerManagers,
        ISmartBpRecognitionSettingsService recognitionSettingsService,
        ISmartBpDebugLog aiDebugLog,
        ISmartBpPromptProfileProvider promptProfileProvider,
        ILlamaCppRuntimeAssetManager llamaRuntimeAssetManager,
        ISmartBpAutoRecognitionCoordinator autoRecognitionCoordinator,
        IGameGuidanceService gameGuidanceService,
        ISmartBpCharacterResolver smartBpCharacterResolver,
        ISmartBpRecognitionRegionProfileService aiRegionProfileService,
        ISmartBpRecognitionLedger aiRecognitionLedger,
        ISmartBpRecognitionStateStore aiRecognitionStateStore,
        ILlamaCppRuntimeUpdateService llamaRuntimeUpdateService,
        ITesseractDataAssetManager tesseractDataAssetManager,
        IRapidOcrModelAssetManager rapidOcrModelAssetManager,
        ISmartBpAutoRecognitionGlobalControlSink autoRecognitionGlobalControl,
        ISmartBpOcrBpRecognitionService ocrBpRecognitionService,
        ISmartBpAiPerformanceMonitor aiPerformanceMonitor,
        ISmartBpModuleStorageProvider smartBpModuleStorage,
        ILogger<SmartBpModuleContentViewModel> logger)
    {
        _logger = logger;
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _regionConfigService = regionConfigService;
        _gameDataSceneDefinition = sceneDefinitions.FirstOrDefault(s =>
                string.Equals(s.SceneKey, SmartBpSceneKeys.GameData, StringComparison.OrdinalIgnoreCase));
        if (_gameDataSceneDefinition == null)
        {
            _logger.LogError("Missing SmartBp scene definition: GameData");
            throw new InvalidOperationException("Missing SmartBp scene definition: GameData");
        }
        _filePickerService = filePickerService;
        _qwenAssetManager = qwenAssetManager;
        _llamaServerManager = llamaServerManager;
        _llamaServerManagers = llamaServerManagers;
        _recognitionSettingsService = recognitionSettingsService;
        _aiDebugLog = aiDebugLog;
        _promptProfileProvider = promptProfileProvider;
        _llamaRuntimeAssetManager = llamaRuntimeAssetManager;
        _autoRecognitionCoordinator = autoRecognitionCoordinator;
        _gameGuidanceService = gameGuidanceService;
        _smartBpCharacterResolver = smartBpCharacterResolver;
        _aiRegionProfileService = aiRegionProfileService;
        _aiRecognitionLedger = aiRecognitionLedger;
        _aiRecognitionStateStore = aiRecognitionStateStore;
        _llamaRuntimeUpdateService = llamaRuntimeUpdateService;
        _tesseractDataAssetManager = tesseractDataAssetManager;
        _rapidOcrModelAssetManager = rapidOcrModelAssetManager;
        _autoRecognitionGlobalControl = autoRecognitionGlobalControl;
        _ocrBpRecognitionService = ocrBpRecognitionService;
        _aiPerformanceMonitor = aiPerformanceMonitor;
        _smartBpModuleStorage = smartBpModuleStorage;
        InitializeAiRecognition();
        _ocrService.DownloadStateChanged += OcrService_DownloadStateChanged;
        // 配置被保存/导入/重置时同步刷新比例状态展示。
        _regionConfigService.GameDataProfileChanged += (_, _) => RunOnUiThread(RefreshRegionAspectInfo);
        _captureAspectRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _captureAspectRefreshTimer.Tick += (_, _) =>
        {
            if (!_windowCaptureService.IsCapturing)
            {
                _captureAspectRefreshTimer.Stop();
                return;
            }

            RefreshRegionAspectInfo();
        };

        ActiveWindows = _windowCaptureService.ListActiveWindows();

        if (!IsWgcHwndCaptureSupported())
        {
            SelectedCaptureMethod = CaptureMethod.Bitblt;
            SelectedCaptureMethodIndex = 1;
        }

        RefreshOcrModelStatus();
        SyncDownloadStateFromService();
        RefreshRegionAspectInfo();
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
    /// 当前识别区域配置文件路径。
    /// </summary>
    [ObservableProperty]
    public partial string RegionConfigPath { get; set; } = "-";

    /// <summary>
    /// 识别区域配置的比例文本（如 16:9）。
    /// </summary>
    [ObservableProperty]
    public partial string RegionConfigAspectRatioText { get; set; } = "-";

    /// <summary>
    /// 当前捕获画面比例文本（如 16:9）。
    /// </summary>
    [ObservableProperty]
    public partial string CaptureAspectRatioText { get; set; } = "-";

    /// <summary>
    /// 区域比例状态文本。
    /// </summary>
    [ObservableProperty]
    public partial string RegionAspectStatusText { get; set; } = "-";

    /// <summary>
    /// 区域比例提示文本。
    /// </summary>
    [ObservableProperty]
    public partial string RegionAspectHintText { get; set; } = "-";

    /// <summary>
    /// 区域比例是否不匹配。
    /// </summary>
    [ObservableProperty]
    public partial bool RegionAspectIsMismatch { get; set; }

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
        if (_windowCaptureService.IsCapturing)
            _captureAspectRefreshTimer.Start();
        // 捕获状态变化会影响多个按钮的可用性和比例提示。
        RefreshCommandStates();
        RefreshRegionAspectInfo();
    }

    /// <summary>
    /// 停止当前窗口捕获，并停止捕获比例刷新计时器。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCaptureStopped))]
    private void StopCapture()
    {
        _windowCaptureService.StopCapture();
        _captureAspectRefreshTimer.Stop();
        RefreshCommandStates();
        RefreshRegionAspectInfo();
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
            if (_windowCaptureService.IsCapturing)
                _captureAspectRefreshTimer.Start();
        }

        RefreshCommandStates();
        RefreshRegionAspectInfo();
    }

    /// <summary>
    /// 打开赛后数据 OCR 区域编辑器，并在保存后写入当前 GameData profile。
    /// </summary>
    /// <returns>区域编辑窗口关闭后的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanOpenRegionEditor))]
    private async Task OpenGameDataRegionEditorAsync()
    {
        // 识别区域编辑依赖当前帧快照，因此必须先启动捕获。
        if (!_windowCaptureService.IsCapturing)
        {
            await MessageBoxHelper.ShowInfoAsync(ResolveLocalizedOrRaw("SmartBpRegionEditorRequireCaptureFirst"));
            return;
        }

        // 编辑器仅使用单帧冻结图像，不做实时刷新。
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
            return;

        var profile = _regionConfigService.GetCurrentGameDataProfile();
        // 保存编辑基准尺寸/比例，便于后续页面匹配展示与诊断。
        profile.BaseAspectRatio = SmartBpRegionConfigService.ToAspectRatioText(frame.PixelWidth, frame.PixelHeight);
        profile.BaseSize = SmartBpRegionConfigService.ToAspectBaseSize(frame.PixelWidth, frame.PixelHeight);

        // 配置已是通用布局结构；这里仅注入编辑展示元数据（标签/模板组）。
        var layout = _gameDataSceneDefinition.BuildEditorLayout(profile.Layout);
        var editor = new RegionEditorWindow(frame, layout)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow
        };

        if (editor.ShowDialog() != true || editor.ResultLayout == null)
            return;

        // 保存前做结构校验，避免非法布局污染识别流程。
        if (!_gameDataSceneDefinition.TryValidateEditedLayout(editor.ResultLayout, out var applyError))
        {
            await MessageBoxHelper.ShowErrorAsync(applyError);
            return;
        }

        profile.Layout = _gameDataSceneDefinition.NormalizeEditedLayoutForPersistence(editor.ResultLayout);

        if (!_regionConfigService.TrySaveGameDataProfile(profile, out var error))
        {
            await MessageBoxHelper.ShowErrorAsync(error);
            return;
        }

        RefreshRegionAspectInfo();
        await MessageBoxHelper.ShowInfoAsync(ResolveLocalizedOrRaw("SmartBpRegionConfigSaved"));
    }

    /// <summary>
    /// 从外部 JSON 文件导入 GameData OCR 区域配置。
    /// </summary>
    /// <returns>导入流程完成后的任务。</returns>
    [RelayCommand]
    private async Task ImportGameDataRegionConfigAsync()
    {
        // 允许导入外部 JSON，校验由配置服务统一处理。
        var file = _filePickerService.PickJsonFile();
        if (string.IsNullOrWhiteSpace(file))
            return;

        if (!_regionConfigService.TryImportGameDataProfile(file, out var error))
        {
            await MessageBoxHelper.ShowErrorAsync(error);
            return;
        }

        RefreshRegionAspectInfo();
        await MessageBoxHelper.ShowInfoAsync(ResolveLocalizedOrRaw("SmartBpRegionConfigImported"));
    }

    /// <summary>
    /// 将当前 GameData OCR 区域配置导出为 JSON 文件。
    /// </summary>
    /// <returns>导出流程完成后的任务。</returns>
    [RelayCommand]
    private async Task ExportGameDataRegionConfigAsync()
    {
        var file = _filePickerService.SaveJsonFile("GameDataRegions.json");
        if (string.IsNullOrWhiteSpace(file))
            return;

        if (!_regionConfigService.TryExportGameDataProfile(file, out var error))
        {
            await MessageBoxHelper.ShowErrorAsync(error);
            return;
        }

        await MessageBoxHelper.ShowInfoAsync(
            string.Format(I18nHelper.GetLocalizedString("SaveSuccessfullyTo"), file));
    }

    /// <summary>
    /// 将 GameData OCR 区域配置重置为模块内置默认值。
    /// </summary>
    /// <returns>重置流程完成后的任务。</returns>
    [RelayCommand]
    private async Task ResetGameDataRegionConfigAsync()
    {
        // 重置来自内置 16:9 默认模板，会覆盖用户当前配置。
        var confirmed = await MessageBoxHelper.ShowConfirmAsync(
            ResolveLocalizedOrRaw("SmartBpRegionConfigResetConfirm"),
            ResolveLocalizedOrRaw("SmartBpRegionConfigResetTitle"),
            ResolveLocalizedOrRaw("Confirm"),
            ResolveLocalizedOrRaw("Cancel"));
        if (!confirmed)
            return;

        if (!_regionConfigService.TryResetGameDataToBuiltinDefault(out var error))
        {
            await MessageBoxHelper.ShowErrorAsync(error);
            return;
        }

        RefreshRegionAspectInfo();
        await MessageBoxHelper.ShowInfoAsync(ResolveLocalizedOrRaw("SmartBpRegionConfigResetDone"));
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

    private bool CanCaptureStopped() => _windowCaptureService.IsCapturing;

    private bool CanOpenPreviewWindow() => _windowCaptureService.IsCapturing;

    private static bool CanOpenWindowPicker() => IsWgcSupported();
    private bool CanOpenRegionEditor() => _windowCaptureService.IsCapturing;

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
        OpenGameDataRegionEditorCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 刷新当前捕获比例与 GameData 区域配置比例的匹配提示。
    /// </summary>
    private void RefreshRegionAspectInfo()
    {
        // 页面显示的比例信息全部来自配置服务，避免 UI 层重复计算逻辑。
        var captureAspect = GetCurrentCaptureAspectRatio();
        var aspect = _regionConfigService.GetAspectInfo(captureAspect);
        RegionConfigPath = aspect.ConfigPath;
        RegionConfigAspectRatioText = aspect.ConfigAspectRatio;
        CaptureAspectRatioText = aspect.CurrentCaptureAspectRatio;

        if (!_windowCaptureService.IsCapturing)
        {
            RegionAspectStatusText = ResolveLocalizedOrRaw("SmartBpRegionAspectStatusNotStarted");
            RegionAspectHintText = ResolveLocalizedOrRaw("SmartBpRegionAspectHintNotStarted");
            RegionAspectIsMismatch = false;
            return;
        }

        // 刚启动捕获时首帧可能尚未到达，此时不应误判为“不匹配”。
        if (string.IsNullOrWhiteSpace(captureAspect) || captureAspect == "-")
        {
            RegionAspectStatusText = ResolveLocalizedOrRaw("SmartBpRegionAspectStatusWaitingFirstFrame");
            RegionAspectHintText = ResolveLocalizedOrRaw("SmartBpRegionAspectHintWaitingFirstFrame");
            RegionAspectIsMismatch = false;
            return;
        }

        if (aspect.IsMatched)
        {
            RegionAspectStatusText = ResolveLocalizedOrRaw("SmartBpRegionAspectStatusMatched");
            RegionAspectHintText = ResolveLocalizedOrRaw("SmartBpRegionAspectHintMatched");
            RegionAspectIsMismatch = false;
            return;
        }

        RegionAspectStatusText = ResolveLocalizedOrRaw("SmartBpRegionAspectStatusMismatched");
        RegionAspectHintText = ResolveLocalizedOrRaw("SmartBpRegionAspectHintMismatched");
        RegionAspectIsMismatch = true;
    }

    /// <summary>
    /// 获取当前捕获帧比例文本（如 16:9）。
    /// 若未捕获或帧不可用，返回 "-" 供界面展示。
    /// </summary>
    /// <summary>
    /// 获取当前捕获帧的宽高比例文本。
    /// </summary>
    /// <returns>比例文本；当前无捕获帧时返回 <see langword="null"/>。</returns>
    private string? GetCurrentCaptureAspectRatio()
    {
        if (!_windowCaptureService.IsCapturing)
            return "-";

        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null || frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
            return "-";

        return SmartBpRegionConfigService.ToAspectRatioText(frame.PixelWidth, frame.PixelHeight);
    }

    /// <summary>
    /// 处理 OCR 下载状态变化，并切回 UI 线程同步绑定属性。
    /// </summary>
    private void OcrService_DownloadStateChanged(object? sender, EventArgs e)
    {
        RunOnUiThread(SyncDownloadStateFromService);
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
