using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using neo_bpsys_wpf.SmartBp.Module.Services.Recognition;
using neo_bpsys_wpf.Views.Windows;

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
    [ObservableProperty] private bool _playBackfillAnimations;
    [ObservableProperty] private string _aiStageDetectionResult = "-";
    [ObservableProperty] private string _aiGuidanceSnapshot = "-";
    [ObservableProperty] private string _aiCandidateOperations = "-";
    [ObservableProperty] private BitmapSource? _aiPhaseCropPreview;
    [ObservableProperty] private BitmapSource? _aiFocusedCropPreview;
    [ObservableProperty] private string _aiCropDebugInfo = "-";
    private SmartBpRecognitionLayoutProfile? _aiRegionProfile;

    private void InitializeAiRecognition()
    {
        SelectedAiTestFrame = AiTestFrames[0];
        QwenManifestStatus = ResolveLocalizedOrRaw("SmartBpAiStatusLoading");
        LlamaServerStatus = ResolveLocalizedOrRaw("SmartBpAiStatusStopped");
        LlamaServerExecutablePath = _recognitionSettingsService.Settings.LlamaServerExecutablePath;
        EnableAutoGuidanceSync = _recognitionSettingsService.Settings.EnableAutoGuidanceSync;
        EnableAutoApplyRecognition = _recognitionSettingsService.Settings.EnableAutoApplyRecognition;
        PlayBackfillAnimations = _recognitionSettingsService.Settings.PlayBackfillAnimations;
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
        try { await RunRegionGatedFrameCoreAsync(LoadTestFrame(SelectedAiTestFrame)); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private Task RecognizeCurrentCaptureFrameAsync() => RecognizeCurrentFrameCoreAsync();
    [RelayCommand] private async Task DetectStageFromSelectedTestFrameAsync()
    {
        if (SelectedAiTestFrame == null) return;
        try { await RunRegionGatedFrameCoreAsync(LoadTestFrame(SelectedAiTestFrame)); }
        catch (Exception ex) { AiLastError = ex.Message; }
    }
    [RelayCommand] private async Task DetectStageFromCurrentCaptureFrameAsync()
    {
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null) { AiLastError = "No capture frame is available."; return; }
        await RunRegionGatedFrameCoreAsync(frame);
    }
    [RelayCommand] private Task RunAutomaticOneTickAsync() => RunAutomaticCurrentFrameCoreAsync();
    [RelayCommand] private async Task StartAiPreviewLoopAsync() { if (!_windowCaptureService.IsCapturing) { AiLastError = "Start capture before starting the recognition loop."; return; } await _autoRecognitionCoordinator.StartAsync(); IsAiPreviewLoopRunning = true; _aiPreviewTimer.Start(); }
    [RelayCommand] private async Task StopAiPreviewLoopAsync() { _aiPreviewTimer.Stop(); await _autoRecognitionCoordinator.StopAsync(); IsAiPreviewLoopRunning = false; }
    private async Task RecognizeCurrentFrameCoreAsync() { var frame = _windowCaptureService.GetCurrentFrame(); if (frame == null) { AiLastError = "No capture frame is available."; return; } await RunRegionGatedFrameCoreAsync(frame); }

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
        }
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
        AiLastError = result.Error ?? "";
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

    partial void OnPlayBackfillAnimationsChanged(bool value)
    {
        _recognitionSettingsService.Settings.PlayBackfillAnimations = value;
        _ = _recognitionSettingsService.SaveAsync();
    }

    [RelayCommand]
    private void ResetAiRecognitionLedger()
    {
        _aiRecognitionLedger.ResetForCurrentGame();
        AiCandidateOperations = ResolveLocalizedOrRaw("SmartBpAiLedgerResetCompleted");
        _aiDebugLog.Write("Recognition", "Recognition ledger reset for the current game.");
    }

    private async Task SaveRuntimeSelectionAsync()
    {
        await _recognitionSettingsService.SaveAsync();
        await RefreshLlamaRuntimeStatusAsync();
    }
}
