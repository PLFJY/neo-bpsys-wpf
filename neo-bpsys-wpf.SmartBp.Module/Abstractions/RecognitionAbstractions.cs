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
    /// <summary>Deletes installed assets.</summary>
    void Delete();
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
public interface ILlamaCppOpenAiClient { /// <summary>Recognizes one image.</summary>
    Task<string> RecognizeAsync(string imageDataUrl, SmartBpRecognitionTask task, CancellationToken cancellationToken = default); }
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
