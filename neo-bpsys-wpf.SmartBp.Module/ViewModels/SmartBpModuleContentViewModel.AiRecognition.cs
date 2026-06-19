using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

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
    [ObservableProperty] private IReadOnlyList<SmartBpPromptProfile> _aiPromptProfiles = [];
    [ObservableProperty] private SmartBpPromptProfile? _selectedAiPromptProfile;
    [ObservableProperty] private IReadOnlyList<LlamaCppRuntimeAsset> _llamaRuntimeAssets = [];
    [ObservableProperty] private LlamaCppRuntimeAsset? _selectedLlamaRuntimeAsset;
    [ObservableProperty] private bool _isLlamaRuntimeInstalled;
    [ObservableProperty] private bool _isLlamaRuntimeDownloading;
    [ObservableProperty] private double _llamaRuntimeDownloadProgress;
    [ObservableProperty] private string _llamaRuntimeDownloadStatus = "-";
    [ObservableProperty] private string _managedLlamaServerExecutablePath = "-";
    [ObservableProperty] private bool _enableAutoGuidanceSync;
    [ObservableProperty] private bool _enableAutoApplyRecognition;
    [ObservableProperty] private string _aiStageDetectionResult = "-";
    [ObservableProperty] private string _aiGuidanceSnapshot = "-";
    [ObservableProperty] private string _aiCandidateOperations = "-";

    private void InitializeAiRecognition()
    {
        SelectedAiTestFrame = AiTestFrames[0];
        QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusLoading");
        LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped");
        LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath;
        EnableAutoGuidanceSync = _recognitionSettingsService.Settings.EnableAutoGuidanceSync;
        EnableAutoApplyRecognition = _recognitionSettingsService.Settings.EnableAutoApplyRecognition;
        _aiPreviewTimer.Interval = TimeSpan.FromMilliseconds(_recognitionSettingsService.Settings.RecognitionIntervalMs);
        _aiPreviewTimer.Tick += async (_, _) => await RunAutomaticCurrentFrameCoreAsync();
        _qwenAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsQwenDownloading = state.IsDownloading; QwenDownloadProgress = state.Progress ?? 0; QwenDownloadStatus = ResolveLocalizedOrRaw(state.Status);
        });
        _aiDebugLog.MessageWritten += (_, message) => RunOnUiThread(() => AppendAiDebugMessage(message));
        _aiDebugLog.Write("SmartBP", "AI recognition diagnostics initialized.");
        _llamaRuntimeAssetManager.StateChanged += (_, state) => RunOnUiThread(() =>
        {
            IsLlamaRuntimeDownloading = state.IsDownloading;
            LlamaRuntimeDownloadProgress = state.Progress ?? 0;
            LlamaRuntimeDownloadStatus = ResolveLocalizedOrRaw(state.Status);
        });
        _ = InitializeAiOptionsAsync();
        _ = RefreshQwenStatusAsync();
    }

    private async Task InitializeAiOptionsAsync()
    {
        try
        {
            AiPromptProfiles = await _promptProfileProvider.GetAvailableProfilesAsync();
            SelectedAiPromptProfile = AiPromptProfiles.FirstOrDefault(x => x.Id == _recognitionSettingsService.Settings.PromptProfileId) ?? AiPromptProfiles.FirstOrDefault();
            LlamaRuntimeAssets = await _llamaRuntimeAssetManager.GetAvailableAssetsAsync();
            SelectedLlamaRuntimeAsset = await _llamaRuntimeAssetManager.GetSelectedAssetAsync();
            await RefreshLlamaRuntimeStatusAsync();
        }
        catch (Exception ex) { AiLastError = ex.Message; }
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
    [RelayCommand] private async Task DeleteQwenModelAsync() { try { if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before deleting the model."); await _qwenAssetManager.DeleteAsync(); IsQwenInstalled = false; } catch (Exception ex) { AiLastError = ex.Message; } }
    [RelayCommand] private async Task BrowseLlamaServerAsync() { var path = _filePickerService.PickExecutableFile(); if (path == null) return; LlamaServerExecutablePath = path; _recognitionSettingsService.Settings.LlamaServerExecutablePath = path; await _recognitionSettingsService.SaveAsync(); }
    [RelayCommand] private async Task DownloadLlamaRuntimeAsync() { try { await _llamaRuntimeAssetManager.InstallAsync(); LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath; await RefreshLlamaRuntimeStatusAsync(); } catch (OperationCanceledException) { } catch (Exception ex) { AiLastError = ex.Message; } }
    [RelayCommand] private void CancelLlamaRuntimeDownload() => _llamaRuntimeAssetManager.Cancel();
    [RelayCommand] private async Task DeleteLlamaRuntimeAsync() { try { if (_llamaServerManager.IsRunning) throw new InvalidOperationException("Stop llama-server before deleting the runtime."); await _llamaRuntimeAssetManager.DeleteAsync(); LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath; await RefreshLlamaRuntimeStatusAsync(); } catch (Exception ex) { AiLastError = ex.Message; } }
    [RelayCommand] private async Task RefreshLlamaRuntimeStatusAsync() { IsLlamaRuntimeInstalled = await _llamaRuntimeAssetManager.IsInstalledAsync(); ManagedLlamaServerExecutablePath = IsLlamaRuntimeInstalled ? await _llamaRuntimeAssetManager.GetInstalledExecutablePathAsync() : "-"; }
    [RelayCommand] private async Task StartLlamaServerAsync() { try { await _llamaServerManager.StartAsync(); LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusReady"); } catch (Exception ex) { LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusFailed"); AiLastError = ex.Message; } }
    [RelayCommand] private async Task StopLlamaServerAsync() { await StopAiPreviewLoopAsync(); await _llamaServerManager.StopAsync(); LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped"); }
    [RelayCommand] private async Task RecognizeSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try { var path = Path.Combine(AppConstants.ResourcesPath, "SmartBp", "TestFrames", SelectedAiTestFrame.FileName); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); await RecognizeCoreAsync(image, SelectedAiTestFrame.Task); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private Task RecognizeCurrentCaptureFrameAsync() => RecognizeCurrentFrameCoreAsync();
    [RelayCommand] private async Task DetectStageFromSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try { await DetectStageCoreAsync(LoadTestFrame(SelectedAiTestFrame)); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private async Task DetectStageFromCurrentCaptureFrameAsync()
    {
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null) { AiLastError = "No capture frame is available."; return; }
        await DetectStageCoreAsync(frame);
    }
    [RelayCommand] private Task RunAutomaticOneTickAsync() => RunAutomaticCurrentFrameCoreAsync();
    [RelayCommand] private async Task StartAiPreviewLoopAsync() { if (!_windowCaptureService.IsCapturing) { AiLastError = "Start capture before starting the recognition loop."; return; } await _autoRecognitionCoordinator.StartAsync(); IsAiPreviewLoopRunning = true; _aiPreviewTimer.Start(); }
    [RelayCommand] private async Task StopAiPreviewLoopAsync() { _aiPreviewTimer.Stop(); await _autoRecognitionCoordinator.StopAsync(); IsAiPreviewLoopRunning = false; }
    private async Task RecognizeCurrentFrameCoreAsync() { var frame = _windowCaptureService.GetCurrentFrame(); if (frame == null) { AiLastError = "No capture frame is available."; return; } await RecognizeCoreAsync(frame, SmartBpRecognitionTask.FullBpScan); }

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
            AiRawResponse = result.RawJson;
            AiStageDetectionResult = result.BusinessState == null ? "-" : FormatBusinessState(result.BusinessState);
            AiGuidanceSnapshot = FormatGuidance(result.GuidanceSnapshot, result.GuidanceSync?.Reason);
            AiCandidateOperations = result.Operations.Count == 0 ? "-" : string.Join(Environment.NewLine, result.Operations.Select(FormatOperation));
            AiParsedVisualResult = AiStageDetectionResult;
            AiNormalizedResult = AiCandidateOperations;
            AiLastError = result.Error ?? "";
        }
        finally { IsAiRecognizing = false; Interlocked.Exchange(ref _recognitionBusy, 0); }
    }

    private static BitmapSource LoadTestFrame(SmartBpTestFrame frame)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(Path.Combine(AppConstants.ResourcesPath, "SmartBp", "TestFrames", frame.FileName));
        image.EndInit(); image.Freeze(); return image;
    }

    private static string FormatStage(SmartBpStageDetectionResult value) =>
        $"action={value.RecognizedAction}; activeSide={value.ActiveSide}; region={value.OperationRegion}; owner={value.OperationOwner}; targetCamp={value.TargetCamp}; confidence={value.Confidence:0.00}{Environment.NewLine}" +
        $"leftTopTitle={value.LeftTopTitle ?? "null"}; rightTopTitle={value.RightTopTitle ?? "null"}; status={value.MainStatus ?? "null"}{Environment.NewLine}" +
        $"evidence={string.Join(" | ", value.Evidence)}{Environment.NewLine}warnings={string.Join(" | ", value.Warnings)}";

    private string FormatBusinessState(SmartBpBusinessStateRecognitionResult value) =>
        SmartBpBusinessStateFormatter.Format(value, _smartBpCharacterResolver, includeResolved: true);

    private static string FormatGuidance(Core.Models.GameGuidanceRuntimeSnapshot value, string? reason = null) =>
        $"started={value.IsStarted}; step={value.CurrentStepIndex}; action={value.CurrentAction?.ToString() ?? "null"}; indexes=[{string.Join(", ", value.CurrentIndexes)}]; time={value.CurrentTime?.ToString() ?? "null"}{Environment.NewLine}{reason ?? ""}";

    private static string FormatOperation(SmartBpDetectedOperation value) =>
        $"{value.Kind}: action={value.SourceGuidanceAction}; indexes=[{string.Join(", ", value.SourceGuidanceIndexes)}]; camp={value.Camp}; slot={value.SlotIndex}; raw={value.RawCharacterName ?? "null"}; resolved={value.ResolvedCharacterName ?? "unresolved"}; playerId={value.PlayerId ?? "null"}; confidence={value.Confidence:0.00}; {value.Reason}";

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
        _ = _recognitionSettingsService.SaveAsync();
    }

    partial void OnSelectedLlamaRuntimeAssetChanged(LlamaCppRuntimeAsset? value)
    {
        if (value == null || value.Id == _recognitionSettingsService.Settings.SelectedLlamaRuntimeId) return;
        _recognitionSettingsService.Settings.SelectedLlamaRuntimeId = value.Id;
        _recognitionSettingsService.Settings.LlamaServerExecutablePath = "";
        LlamaServerExecutablePath = "";
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

    private async Task SaveRuntimeSelectionAsync()
    {
        await _recognitionSettingsService.SaveAsync();
        await RefreshLlamaRuntimeStatusAsync();
    }
}
