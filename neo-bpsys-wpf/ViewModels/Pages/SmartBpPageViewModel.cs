using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using Windows.Graphics.Capture;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SmartBpPageViewModel : ViewModelBase
{
    private readonly IWindowCaptureService _windowCaptureService = null!;

    public SmartBpPageViewModel()
    {
        // Decorative constructor for design-time only.
    }

    public SmartBpPageViewModel(IWindowCaptureService windowCaptureService)
    {
        _windowCaptureService = windowCaptureService;
        ActiveWindows = _windowCaptureService.ListActiveWindows();

        if (!GraphicsCaptureSession.IsSupported())
        {
            SelectedCaptureMethod = CaptureMethod.Bitblt;
            SelectedCaptureMethodIndex = 1;
        }
    }

    [ObservableProperty]
    private List<WindowInfo> _activeWindows = [];

    private WindowInfo? _selectedWindow;

    public WindowInfo? SelectedWindow {
        get => _selectedWindow;
        set => SetPropertyWithAction(ref _selectedWindow, value, _ =>
        {
            StartCaptureCommand.NotifyCanExecuteChanged();
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

    private bool CanCaptureStarted() =>
        SelectedCaptureMethod == CaptureMethod.WGC ? GraphicsCaptureSession.IsSupported() : SelectedWindow is not null;

    private bool CanCaptureStopped() => _windowCaptureService.IsCapturing;

    private bool CanOpenPreviewWindow() => _windowCaptureService.IsCapturing;

    private static bool CanOpenWindowPicker() => GraphicsCaptureSession.IsSupported();

    private void RefreshCommandStates()
    {
        StopCaptureCommand.NotifyCanExecuteChanged();
        OpenPreviewWindowCommand.NotifyCanExecuteChanged();
        StartCaptureCommand.NotifyCanExecuteChanged();
        OpenWindowPickerCommand.NotifyCanExecuteChanged();
    }

    public CaptureMethod SelectedCaptureMethod { get; set; } = CaptureMethod.WGC;

    public int SelectedCaptureMethodIndex { get; set; }

    public List<CaptureMethodSelection> CaptureMethodList { get; } =
    [
        new(CaptureMethod.WGC, "Windows Graphic Capture (Windows 10 1809 or later)"),
        new(CaptureMethod.Bitblt, "Bitblt")
    ];

    public class CaptureMethodSelection
    {
        public CaptureMethodSelection(CaptureMethod method, string displayName)
        {
            Method = method;
            DisplayName = displayName;
            if (method == CaptureMethod.WGC && !GraphicsCaptureSession.IsSupported())
            {
                IsAvaliable = false;
            }
        }

        public CaptureMethod Method { get; init; }

        public string DisplayName { get; init; }

        public bool IsAvaliable { get; init; } = true;
    }
}
