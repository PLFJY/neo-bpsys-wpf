using System.Windows.Media.Imaging;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Abstractions;

/// <summary>Loads bundled Qwen metadata.</summary>
public interface IQwenModelManifestProvider { /// <summary>Loads and validates the manifest.</summary>
    Task<QwenModelManifest> LoadAsync(CancellationToken cancellationToken = default); }
/// <summary>Installs and removes Qwen model assets.</summary>
public interface IQwenModelAssetManager
{
    /// <summary>Raised when download state changes.</summary>
    event EventHandler<QwenDownloadState>? StateChanged;
    /// <summary>Gets current download state.</summary>
    QwenDownloadState State { get; }
    /// <summary>Gets the selected profile.</summary>
    Task<QwenModelProfile> GetProfileAsync(CancellationToken cancellationToken = default);
    /// <summary>Checks installed assets, including hashes.</summary>
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    /// <summary>Downloads missing assets.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);
    /// <summary>Cancels an active download.</summary>
    void Cancel();
    /// <summary>Deletes installed assets without blocking the caller thread.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets installed model and projector paths.</summary>
    Task<(string ModelPath, string MmprojPath)> GetInstalledPathsAsync(CancellationToken cancellationToken = default);
}
/// <summary>Persists recognition settings.</summary>
public interface ISmartBpRecognitionSettingsService
{
    /// <summary>Gets current settings.</summary>
    SmartBpRecognitionSettings Settings { get; }
    /// <summary>Saves current settings.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
/// <summary>Loads bundled SmartBP recognition prompt profiles.</summary>
public interface ISmartBpPromptProfileProvider
{
    /// <summary>Gets available bundled profiles.</summary>
    Task<IReadOnlyList<SmartBpPromptProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>Loads one profile by id.</summary>
    Task<SmartBpPromptProfile> LoadAsync(string profileId, CancellationToken cancellationToken = default);
}
/// <summary>Loads llama.cpp runtime metadata.</summary>
public interface ILlamaCppRuntimeManifestProvider
{
    /// <summary>Loads and validates the bundled runtime manifest.</summary>
    Task<LlamaCppRuntimeManifest> LoadAsync(CancellationToken cancellationToken = default);
}
/// <summary>Installs and manages a selected llama.cpp runtime.</summary>
public interface ILlamaCppRuntimeAssetManager
{
    /// <summary>Raised when installation state changes.</summary>
    event EventHandler<LlamaCppRuntimeInstallState>? StateChanged;
    /// <summary>Gets current installation state.</summary>
    LlamaCppRuntimeInstallState State { get; }
    /// <summary>Gets selectable runtime assets.</summary>
    Task<IReadOnlyList<LlamaCppRuntimeAsset>> GetAvailableAssetsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets the selected runtime asset.</summary>
    Task<LlamaCppRuntimeAsset> GetSelectedAssetAsync(CancellationToken cancellationToken = default);
    /// <summary>Checks whether the selected runtime is installed.</summary>
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    /// <summary>Installs the selected runtime.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);
    /// <summary>Cancels an active installation.</summary>
    void Cancel();
    /// <summary>Deletes the selected installed runtime without blocking the caller thread.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets the selected installed server executable.</summary>
    Task<string> GetInstalledExecutablePathAsync(CancellationToken cancellationToken = default);
}
/// <summary>Controls the local llama.cpp server.</summary>
public interface ILlamaCppServerManager
{
    /// <summary>Gets whether the managed process is ready.</summary>
    bool IsRunning { get; }
    /// <summary>Gets a display status.</summary>
    string Status { get; }
    /// <summary>Starts and awaits readiness.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops the managed process.</summary>
    Task StopAsync();
}
/// <summary>Encodes WPF frames for multimodal requests.</summary>
public interface ISmartBpImageEncoder { /// <summary>Encodes a PNG data URL.</summary>
    string EncodeDataUrl(BitmapSource source, int maxWidth); }
/// <summary>Sends independent OpenAI-compatible requests.</summary>
public interface ILlamaCppOpenAiClient
{
    /// <summary>Recognizes one image using the manual generic schema.</summary>
    Task<string> RecognizeAsync(string imageDataUrl, SmartBpRecognitionTask task, CancellationToken cancellationToken = default);
    /// <summary>Detects the active BP stage without extracting characters.</summary>
    Task<string> DetectStageAsync(string imageDataUrl, CancellationToken cancellationToken = default);
    /// <summary>Extracts the operation for a locally selected guidance step.</summary>
    Task<string> RecognizeFocusedAsync(string imageDataUrl, Core.Enums.GameAction action, IReadOnlyList<int> indexes, CancellationToken cancellationToken = default);
}
/// <summary>Resolves model names against shared character dictionaries.</summary>
public interface ISmartBpCharacterResolver { /// <summary>Resolves a character name safely.</summary>
    SmartBpNormalizedCharacter Resolve(string? rawName, Core.Enums.Camp camp, int slot, double confidence); }
/// <summary>Runs and normalizes one recognition request.</summary>
public interface ISmartBpAiRecognitionService { /// <summary>Recognizes one frame.</summary>
    Task<SmartBpRecognitionPreview> RecognizeAsync(BitmapSource frame, SmartBpRecognitionTask task, CancellationToken cancellationToken = default); }

/// <summary>Publishes bounded, user-visible diagnostics for the SmartBP AI pipeline.</summary>
public interface ISmartBpDebugLog
{
    /// <summary>Raised whenever a diagnostic line is written.</summary>
    event EventHandler<SmartBpDebugMessageEventArgs>? MessageWritten;
    /// <summary>Writes one diagnostic line.</summary>
    /// <param name="source">Short subsystem name.</param>
    /// <param name="message">Diagnostic message.</param>
    void Write(string source, string message);
}

/// <summary>Reconciles model stage output with the authoritative GameGuidance workflow.</summary>
public interface ISmartBpGuidanceSyncService
{
    /// <summary>Synchronizes to the current or nearest compatible future step.</summary>
    Task<SmartBpGuidanceSyncResult> SyncAsync(SmartBpStageDetectionResult detectedStage, CancellationToken cancellationToken = default);
}

/// <summary>Applies locally validated candidate operations through character selection services.</summary>
public interface ISmartBpDetectedOperationApplier
{
    /// <summary>Applies accepted resolved operations.</summary>
    Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default);
}

/// <summary>Coordinates stage detection, guidance reconciliation and focused extraction.</summary>
public interface ISmartBpAutoRecognitionCoordinator
{
    /// <summary>Gets whether automatic mode is running.</summary>
    bool IsRunning { get; }
    /// <summary>Starts automatic mode.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    /// <summary>Stops automatic mode.</summary>
    Task StopAsync();
    /// <summary>Runs one stage-aware automatic recognition tick.</summary>
    Task<SmartBpAutoRecognitionTickResult> RunOneTickAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}
