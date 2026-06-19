using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SmartBpModuleContentViewModel
{
    private readonly DispatcherTimer _aiPreviewTimer = new();
    private int _recognitionBusy;

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
    [ObservableProperty] private bool _isQwenInstalled;
    [ObservableProperty] private bool _isQwenDownloading;
    [ObservableProperty] private double _qwenDownloadProgress;
    [ObservableProperty] private string _qwenDownloadStatus = "-";
    [ObservableProperty] private string _llamaServerExecutablePath = "";
    [ObservableProperty] private string _llamaServerStatus = "SmartBpAiStatusStopped";
    [ObservableProperty] private bool _isAiRecognizing;
    [ObservableProperty] private bool _isAiPreviewLoopRunning;
    [ObservableProperty] private string _aiRawResponse = "";
    [ObservableProperty] private string _aiNormalizedResult = "";
    [ObservableProperty] private long _aiElapsedMilliseconds;
    [ObservableProperty] private int _aiRecommendedIntervalMilliseconds;
    [ObservableProperty] private string _aiLastError = "";
    [ObservableProperty] private string _aiDebugLogText = "";

    private void InitializeAiRecognition()
    {
        SelectedAiTestFrame = AiTestFrames[0];
        QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusLoading");
        LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped");
        LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath;
        _aiPreviewTimer.Interval = TimeSpan.FromMilliseconds(_recognitionSettingsService.Settings.RecognitionIntervalMs);
        _aiPreviewTimer.Tick += async (_, _) => await RecognizeCurrentFrameCoreAsync();
        _qwenAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsQwenDownloading = state.IsDownloading; QwenDownloadProgress = state.Progress ?? 0; QwenDownloadStatus = ResolveLocalizedOrRaw(state.Status);
        });
        _aiDebugLog.MessageWritten += (_, message) => RunOnUiThread(() => AppendAiDebugMessage(message));
        _aiDebugLog.Write("SmartBP", "AI recognition diagnostics initialized.");
        _ = RefreshQwenStatusAsync();
    }

    [RelayCommand]
    private void ClearAiDebugLog() => AiDebugLogText = "";

    private void AppendAiDebugMessage(SmartBpDebugMessageEventArgs message)
    {
        const int maximumCharacters = 60000;
        AiDebugLogText += $"[{message.Timestamp:HH:mm:ss.fff}] [{message.Source}] {message.Message}{Environment.NewLine}";
        if (AiDebugLogText.Length <= maximumCharacters) return;
        var firstLineBreak = AiDebugLogText.IndexOf(Environment.NewLine, AiDebugLogText.Length - maximumCharacters, StringComparison.Ordinal);
        AiDebugLogText = firstLineBreak >= 0 ? AiDebugLogText[(firstLineBreak + Environment.NewLine.Length)..] : AiDebugLogText[^maximumCharacters..];
    }

    [RelayCommand] private async Task RefreshQwenStatusAsync()
    {
        try { var p = await _qwenAssetManager.GetProfileAsync(); QwenModelProfile = p.DisplayName; QwenMmprojProfile = Path.GetFileNameWithoutExtension(p.MmprojFileName); IsQwenInstalled = await _qwenAssetManager.IsInstalledAsync(); QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusLoaded"); }
        catch (Exception ex) { QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed"); AiLastError = ex.Message; }
    }
    [RelayCommand] private async Task DownloadQwenModelAsync() { try { await _qwenAssetManager.InstallAsync(); await RefreshQwenStatusAsync(); } catch (OperationCanceledException) { } catch (Exception ex) { AiLastError = ex.Message; } }
    [RelayCommand] private void CancelQwenDownload() => _qwenAssetManager.Cancel();
    [RelayCommand] private void DeleteQwenModel() { try { if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before deleting the model."); _qwenAssetManager.Delete(); IsQwenInstalled = false; } catch (Exception ex) { AiLastError = ex.Message; } }
    [RelayCommand] private async Task BrowseLlamaServerAsync() { var path = _filePickerService.PickExecutableFile(); if (path == null) return; LlamaServerExecutablePath = path; _recognitionSettingsService.Settings.LlamaServerExecutablePath = path; await _recognitionSettingsService.SaveAsync(); }
    [RelayCommand] private async Task StartLlamaServerAsync() { try { await _llamaServerManager.StartAsync(); LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusReady"); } catch (Exception ex) { LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed"); AiLastError = ex.Message; } }
    [RelayCommand] private async Task StopLlamaServerAsync() { StopAiPreviewLoop(); await _llamaServerManager.StopAsync(); LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped"); }
    [RelayCommand] private async Task RecognizeSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try { var path = Path.Combine(AppConstants.ResourcesPath, "SmartBp", "TestFrames", SelectedAiTestFrame.FileName); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); await RecognizeCoreAsync(image, SelectedAiTestFrame.Task); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private Task RecognizeCurrentCaptureFrameAsync() => RecognizeCurrentFrameCoreAsync();
    [RelayCommand] private void StartAiPreviewLoop() { if (!_windowCaptureService.IsCapturing) { AiLastError = "Start capture before starting the recognition loop."; return; } IsAiPreviewLoopRunning = true; _aiPreviewTimer.Start(); }
    [RelayCommand] private void StopAiPreviewLoop() { _aiPreviewTimer.Stop(); IsAiPreviewLoopRunning = false; }
    private async Task RecognizeCurrentFrameCoreAsync() { var frame = _windowCaptureService.GetCurrentFrame(); if (frame == null) { AiLastError = "No capture frame is available."; return; } await RecognizeCoreAsync(frame, SmartBpRecognitionTask.FullBpScan); }
    private async Task RecognizeCoreAsync(BitmapSource frame, SmartBpRecognitionTask task)
    {
        if (Interlocked.CompareExchange(ref _recognitionBusy, 1, 0) != 0) return;
        IsAiRecognizing = true; AiLastError = "";
        try { var result = await _aiRecognitionService.RecognizeAsync(frame, task); AiRawResponse = result.RawResponse; AiNormalizedResult = result.NormalizedSummary; AiElapsedMilliseconds = result.ElapsedMilliseconds; AiRecommendedIntervalMilliseconds = result.RecommendedIntervalMilliseconds; AiLastError = result.Error ?? ""; }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }
}
