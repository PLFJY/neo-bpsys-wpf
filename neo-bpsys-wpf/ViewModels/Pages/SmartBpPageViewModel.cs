using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using System.Windows.Threading;
using neo_bpsys_wpf.Core;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SmartBpPageViewModel : ViewModelBase
{
    // WGC API 可用最低版本：Windows 10 1803。
    private const int WgcMinimumBuild = 17134; // Windows 10 1803
    // HWND 直捕（CreateForWindow）可用最低版本：Windows 10 1903。
    private const int WgcHwndInteropMinimumBuild = 18362; // Windows 10 1903

    private readonly IWindowCaptureService _windowCaptureService = null!;
    private readonly IOcrService _ocrService = null!;
    private readonly ISmartBpRegionConfigService _regionConfigService = null!;
    private readonly IFilePickerService _filePickerService = null!;
    private static readonly RegionEditorStructure GameDataEditorStructure = CreateGameDataEditorStructure();
    private readonly DispatcherTimer _captureAspectRefreshTimer;

#pragma warning disable CS8618
    public SmartBpPageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    /// <summary>
    /// SmartBp 页面视图模型构造函数。
    /// </summary>
    public SmartBpPageViewModel(
        IWindowCaptureService windowCaptureService,
        IOcrService ocrService,
        ISmartBpRegionConfigService regionConfigService,
        IFilePickerService filePickerService)
    {
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _regionConfigService = regionConfigService;
        _filePickerService = filePickerService;
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

    [ObservableProperty]
    private List<WindowInfo> _activeWindows = [];

    [ObservableProperty]
    private List<OcrModelSelection> _ocrModelList = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedOcrModelCommand))]
    private OcrModelSelection? _selectedOcrModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedOcrModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SwitchSelectedOcrModelCommand))]
    private bool _isModelDownloading;

    [ObservableProperty]
    private bool _hasPreciseDownloadProgress;

    [ObservableProperty]
    private double _modelDownloadProgress;

    [ObservableProperty]
    private string _modelDownloadProgressText = string.Empty;

    [ObservableProperty]
    private string _modelDownloadStageText = string.Empty;

    [ObservableProperty]
    private string _currentOcrModelDisplayName = "SmartBpCurrentOcrModelNotEnabled";

    [ObservableProperty]
    private string _regionConfigPath = "-";

    [ObservableProperty]
    private string _regionConfigAspectRatioText = "-";

    [ObservableProperty]
    private string _captureAspectRatioText = "-";

    [ObservableProperty]
    private string _regionAspectStatusText = "-";

    [ObservableProperty]
    private string _regionAspectHintText = "-";

    [ObservableProperty]
    private bool _regionAspectIsMismatch;

    public bool ShowDownloadModelButton => SelectedOcrModel is not { IsInstalled: true };

    public bool ShowDeleteModelButton => SelectedOcrModel is { IsInstalled: true };

    private WindowInfo? _selectedWindow;

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

    [RelayCommand]
    private void RefreshActiveWindows() => ActiveWindows = _windowCaptureService.ListActiveWindows();

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

    [RelayCommand(CanExecute = nameof(CanCaptureStopped))]
    private void StopCapture()
    {
        _windowCaptureService.StopCapture();
        _captureAspectRefreshTimer.Stop();
        RefreshCommandStates();
        RefreshRegionAspectInfo();
    }

    [RelayCommand(CanExecute = nameof(CanOpenPreviewWindow))]
    private void OpenPreviewWindow() => _windowCaptureService.OpenPreviewWindow();

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
        var layout = BuildGameDataLayout(profile.Layout, GameDataEditorStructure);
        var editor = new RegionEditorWindow(frame, layout)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow
        };

        if (editor.ShowDialog() != true || editor.ResultLayout == null)
            return;

        // 保存前做结构校验，避免非法布局污染识别流程。
        if (!TryValidateGameDataLayout(editor.ResultLayout, GameDataEditorStructure, out var applyError))
        {
            await MessageBoxHelper.ShowErrorAsync(applyError);
            return;
        }

        profile.Layout = NormalizeGameDataLayoutForPersistence(editor.ResultLayout, GameDataEditorStructure);

        if (!_regionConfigService.TrySaveGameDataProfile(profile, out var error))
        {
            await MessageBoxHelper.ShowErrorAsync(error);
            return;
        }

        RefreshRegionAspectInfo();
        await MessageBoxHelper.ShowInfoAsync(ResolveLocalizedOrRaw("SmartBpRegionConfigSaved"));
    }

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
                    m.DisplayName,
                    m.Description,
                    _ocrService.IsModelInstalled(m.Key),
                    m.Key == currentModelKey))
        ];

        SelectedOcrModel = OcrModelList.FirstOrDefault(m => m.Key == preferredSelectedKey)
            ?? OcrModelList.FirstOrDefault(m => m.Key == currentModelKey)
            ?? OcrModelList.FirstOrDefault(m => m.Key == recommendedModelKey)
            ?? OcrModelList.FirstOrDefault();

        CurrentOcrModelDisplayName = currentModelKey is null
            ? "SmartBpCurrentOcrModelNotEnabled"
            : OcrModelList.FirstOrDefault(m => m.Key == currentModelKey)?.DisplayName ?? currentModelKey;
    }

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

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedOcrModel))]
    private async Task DeleteSelectedOcrModelAsync()
    {
        if (SelectedOcrModel == null)
            return;

        var confirmed = await MessageBoxHelper.ShowConfirmAsync(
            string.Format(I18nHelper.GetLocalizedString("SmartBpDeleteOcrModelConfirmFormat"), ResolveLocalizedOrRaw(SelectedOcrModel.DisplayName)),
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

    [RelayCommand]
    private void CancelOcrModelDownload()
    {
        _ocrService.CancelDownload();
    }

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
        !IsModelDownloading && SelectedOcrModel is { IsInstalled: true };

    private void RefreshCommandStates()
    {
        // 捕获状态变化后，统一刷新和捕获相关的命令可用性。
        StopCaptureCommand.NotifyCanExecuteChanged();
        OpenPreviewWindowCommand.NotifyCanExecuteChanged();
        StartCaptureCommand.NotifyCanExecuteChanged();
        OpenWindowPickerCommand.NotifyCanExecuteChanged();
        OpenGameDataRegionEditorCommand.NotifyCanExecuteChanged();
    }

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
    private string? GetCurrentCaptureAspectRatio()
    {
        if (!_windowCaptureService.IsCapturing)
            return "-";

        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null || frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
            return "-";

        return SmartBpRegionConfigService.ToAspectRatioText(frame.PixelWidth, frame.PixelHeight);
    }

    private void OcrService_DownloadStateChanged(object? sender, EventArgs e)
    {
        RunOnUiThread(SyncDownloadStateFromService);
    }

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

    private static bool IsWgcApiAvailable() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, WgcMinimumBuild);

    private static bool IsWgcHwndInteropAvailable() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, WgcHwndInteropMinimumBuild);

    private static bool IsWgcSupported()
    {
        if (!IsWgcApiAvailable())
            return false;

        return GraphicsCaptureSession.IsSupported();
    }

    private static bool IsWgcHwndCaptureSupported() => IsWgcHwndInteropAvailable() && IsWgcSupported();

    private static string GetRecommendedModelKeyForCurrentLanguage()
    {
        var language = LocalizeDictionary.CurrentCulture.Name;
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return "en-v4-mobile";

        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return "ja-v4-mobile";

        return "zh-cn-v5-mobile";
    }

    private static string ResolveLocalizedOrRaw(string keyOrRawText)
    {
        var localized = I18nHelper.GetLocalizedString(keyOrRawText);
        return string.IsNullOrWhiteSpace(localized) ? keyOrRawText : localized;
    }

    /// <summary>
    /// 为配置布局注入 GameData 编辑所需的展示元数据（标签、模板分组）。
    /// </summary>
    private static RegionLayoutDefinition BuildGameDataLayout(RegionLayoutDefinition sourceLayout, RegionEditorStructure structure)
    {
        var builder = RegionLayoutDefinition.Builder(ResolveLocalizedOrRaw(structure.SceneDisplayName));
        var elementById = structure.Elements.ToDictionary(e => e.Id, StringComparer.Ordinal);

        foreach (var sourceRoot in sourceLayout.Roots)
        {
            var element = elementById.TryGetValue(sourceRoot.Id, out var found)
                ? found
                : new RegionEditorElement(sourceRoot.Id, sourceRoot.Id, []);
            Action<RegionNodeBuilder> configure = node =>
            {
                for (var c = 0; c < sourceRoot.Children.Count; c++)
                {
                    var label = c < element.CellLabels.Count ? element.CellLabels[c] : sourceRoot.Children[c].Id;
                    node.AddChild(
                        sourceRoot.Children[c].Id,
                        ResolveLocalizedOrRaw(label),
                        new RegionNodeConfig
                        {
                            Rect = sourceRoot.Children[c].Rect,
                            ClampToParent = true
                        });
                }
            };

            var rootConfig = new RegionNodeConfig
            {
                Rect = sourceRoot.Rect,
                ClampToParent = true
            };
            if (string.IsNullOrWhiteSpace(element.TemplateGroupId))
            {
                builder.AddNode(sourceRoot.Id, ResolveLocalizedOrRaw(element.Label), rootConfig, configure);
            }
            else
            {
                builder.AddTemplatedNode(element.TemplateGroupId, sourceRoot.Id, ResolveLocalizedOrRaw(element.Label), rootConfig, configure);
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// 校验编辑器返回布局是否满足 GameData 结构约束。
    /// </summary>
    private static bool TryValidateGameDataLayout(
        RegionLayoutDefinition layout,
        RegionEditorStructure structure,
        out string error)
    {
        error = string.Empty;
        if (layout.Roots.Count != structure.Elements.Count)
        {
            error = string.Format(
                ResolveLocalizedOrRaw("SmartBpRegionLayoutMismatchRootCountFormat"),
                structure.Elements.Count,
                layout.Roots.Count);
            return false;
        }

        for (var i = 0; i < structure.Elements.Count; i++)
        {
            var root = layout.Roots[i];
            var expectedId = structure.Elements[i].Id;
            if (!string.Equals(root.Id, expectedId, StringComparison.Ordinal))
            {
                error = string.Format(
                    ResolveLocalizedOrRaw("SmartBpRegionLayoutMismatchRootIdFormat"),
                    i + 1,
                    expectedId,
                    root.Id);
                return false;
            }

            var expectedCellCount = structure.Elements[i].CellLabels.Count;
            if (root.Children.Count != expectedCellCount)
            {
                error = string.Format(
                    ResolveLocalizedOrRaw("SmartBpRegionLayoutMismatchCellCountFormat"),
                    i + 1,
                    expectedCellCount,
                    root.Children.Count);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 将编辑结果标准化为稳定配置结构，避免把当前语言展示文案直接写入配置文件。
    /// </summary>
    private static RegionLayoutDefinition NormalizeGameDataLayoutForPersistence(
        RegionLayoutDefinition layout,
        RegionEditorStructure structure)
    {
        var builder = RegionLayoutDefinition.Builder(structure.SceneDisplayName);

        for (var i = 0; i < layout.Roots.Count; i++)
        {
            var sourceRoot = layout.Roots[i];
            var element = structure.Elements[i];
            Action<RegionNodeBuilder> configure = node =>
            {
                for (var c = 0; c < sourceRoot.Children.Count; c++)
                {
                    var label = c < element.CellLabels.Count ? element.CellLabels[c] : sourceRoot.Children[c].Id;
                    node.AddChild(
                        sourceRoot.Children[c].Id,
                        label,
                        new RegionNodeConfig
                        {
                            Rect = sourceRoot.Children[c].Rect,
                            ClampToParent = true
                        });
                }
            };

            var rootConfig = new RegionNodeConfig
            {
                Rect = sourceRoot.Rect,
                ClampToParent = true
            };
            if (string.IsNullOrWhiteSpace(element.TemplateGroupId))
            {
                builder.AddNode(sourceRoot.Id, element.Label, rootConfig, configure);
            }
            else
            {
                builder.AddTemplatedNode(element.TemplateGroupId, sourceRoot.Id, element.Label, rootConfig, configure);
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// GameData 编辑结构定义。
    /// 你后续只需要改这里，就能调整大元素数量和名称（以及小元素名称）。
    /// </summary>
    private static RegionEditorStructure CreateGameDataEditorStructure()
    {
        const string hunterTemplateGroup = "hunter_rows";
        const string survivorTemplateGroup = "survivor_rows";
        return new RegionEditorStructure(
            "SmartBpSceneGameData",
            [
                // 监管者与求生者拆分为两套模板，避免跨角色误套用。
                new RegionEditorElement("row0_hunter", "SmartBpRegionHunterRow",
                    ["SmartBpRegionCellName", "SmartBpRegionCellHunterRemainingCipher", "SmartBpRegionCellHunterPalletsDestroyed", "SmartBpRegionCellHunterSurvivorHits", "SmartBpRegionCellHunterTerrorShocks", "SmartBpRegionCellHunterKnockdowns"],
                    hunterTemplateGroup),
                new RegionEditorElement("row1_survivor", "SmartBpRegionSurvivorRow1",
                    ["SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress", "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues", "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"],
                    survivorTemplateGroup),
                new RegionEditorElement("row2_survivor", "SmartBpRegionSurvivorRow2",
                    ["SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress", "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues", "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"],
                    survivorTemplateGroup),
                new RegionEditorElement("row3_survivor", "SmartBpRegionSurvivorRow3",
                    ["SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress", "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues", "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"],
                    survivorTemplateGroup),
                new RegionEditorElement("row4_survivor", "SmartBpRegionSurvivorRow4",
                    ["SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress", "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues", "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"],
                    survivorTemplateGroup)
            ]);
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

    private sealed record RegionEditorStructure(string SceneDisplayName, IReadOnlyList<RegionEditorElement> Elements);

    private sealed record RegionEditorElement(
        string Id,
        string Label,
        IReadOnlyList<string> CellLabels,
        string? TemplateGroupId = null);
}
