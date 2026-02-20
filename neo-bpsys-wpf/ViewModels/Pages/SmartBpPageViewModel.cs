using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using Windows.Graphics.Capture;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SmartBpPageViewModel : ViewModelBase
{
    private const int WgcMinimumBuild = 17134; // Windows 10 1803
    private const int WgcHwndInteropMinimumBuild = 18362; // Windows 10 1903

    private readonly IWindowCaptureService _windowCaptureService = null!;
    private readonly IOcrService _ocrService = null!;

    public SmartBpPageViewModel()
    {
        // Decorative constructor for design-time only.
        OcrModelList = [];
    }

    public SmartBpPageViewModel(IWindowCaptureService windowCaptureService, IOcrService ocrService)
    {
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _ocrService.DownloadStateChanged += OcrService_DownloadStateChanged;

        ActiveWindows = _windowCaptureService.ListActiveWindows();

        if (!IsWgcHwndCaptureSupported())
        {
            SelectedCaptureMethod = CaptureMethod.Bitblt;
            SelectedCaptureMethodIndex = 1;
        }

        RefreshOcrModelStatus();
        SyncDownloadStateFromService();
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
        RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanCaptureStopped))]
    private void StopCapture()
    {
        _windowCaptureService.StopCapture();
        RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanOpenPreviewWindow))]
    private void OpenPreviewWindow() => _windowCaptureService.OpenPreviewWindow();

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

    private bool CanDownloadSelectedOcrModel() =>
        !IsModelDownloading && SelectedOcrModel is { IsInstalled: false };

    private bool CanDeleteSelectedOcrModel() =>
        !IsModelDownloading && SelectedOcrModel is { IsInstalled: true };

    private bool CanSwitchSelectedOcrModel() =>
        !IsModelDownloading && SelectedOcrModel is { IsInstalled: true };

    private void RefreshCommandStates()
    {
        StopCaptureCommand.NotifyCanExecuteChanged();
        OpenPreviewWindowCommand.NotifyCanExecuteChanged();
        StartCaptureCommand.NotifyCanExecuteChanged();
        OpenWindowPickerCommand.NotifyCanExecuteChanged();
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

    public CaptureMethod SelectedCaptureMethod { get; set; } = CaptureMethod.WGC;

    public int SelectedCaptureMethodIndex { get; set; }

    public List<CaptureMethodSelection> CaptureMethodList { get; } =
    [
        new(CaptureMethod.WGC, "SmartBpCaptureMethodWgc"),
        new(CaptureMethod.Bitblt, "SmartBpCaptureMethodBitblt")
    ];

    public class CaptureMethodSelection
    {
        public CaptureMethodSelection(CaptureMethod method, string displayNameKey)
        {
            Method = method;
            DisplayNameKey = displayNameKey;

            if (method == CaptureMethod.WGC && !IsWgcHwndCaptureSupported())
            {
                IsAvaliable = false;
            }
        }

        public CaptureMethod Method { get; init; }

        public string DisplayNameKey { get; init; }

        public bool IsAvaliable { get; init; } = true;
    }

    public sealed record OcrModelSelection(
        string Key,
        string DisplayName,
        string Description,
        bool IsInstalled,
        bool IsCurrent);
}
