using System.Text.Json.Serialization;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

/// <summary>Supported AI recognition tasks.</summary>
public enum SmartBpRecognitionTask { BanSur, BanHun, PickSur, PickHun, CharacterDistribution, FullBpScan }

/// <summary>Qwen manifest root.</summary>
public sealed class QwenModelManifest
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Gets or sets model profiles.</summary>
    public List<QwenModelProfile> Models { get; set; } = [];
}

/// <summary>One Qwen model and its matching vision projector.</summary>
public sealed class QwenModelProfile
{
    /// <summary>Gets or sets the profile id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gets or sets the model URL.</summary>
    public string ModelUrl { get; set; } = "";
    /// <summary>Gets or sets the model filename.</summary>
    public string ModelFileName { get; set; } = "";
    /// <summary>Gets or sets the projector URL.</summary>
    public string MmprojUrl { get; set; } = "";
    /// <summary>Gets or sets the projector filename.</summary>
    public string MmprojFileName { get; set; } = "";
    /// <summary>Gets or sets the optional model hash.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Gets or sets the optional projector hash.</summary>
    public string? MmprojSha256 { get; set; }
}

/// <summary>Persisted AI recognition settings.</summary>
public sealed class SmartBpRecognitionSettings
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Gets or sets llama-server path.</summary>
    public string LlamaServerExecutablePath { get; set; } = "";
    /// <summary>Gets or sets the loopback port.</summary>
    public int LlamaServerPort { get; set; } = 18080;
    /// <summary>Gets or sets selected Qwen profile.</summary>
    public string SelectedQwenModelId { get; set; } = "qwen3.5-2b-q4ks";
    /// <summary>Gets or sets the selected projector profile label.</summary>
    public string SelectedMmprojId { get; set; } = "mmproj-f16";
    /// <summary>Gets or sets maximum encoded width.</summary>
    public int MaxImageWidth { get; set; } = 1280;
    /// <summary>Gets or sets image encoding format.</summary>
    public string ImageFormat { get; set; } = "png";
    /// <summary>Gets or sets inference temperature.</summary>
    public double Temperature { get; set; }
    /// <summary>Gets or sets focused token limit.</summary>
    public int FocusedMaxTokens { get; set; } = 128;
    /// <summary>Gets or sets full-scan token limit.</summary>
    public int FullScanMaxTokens { get; set; } = 512;
    /// <summary>Gets or sets loop interval.</summary>
    public int RecognitionIntervalMs { get; set; } = 1200;
    /// <summary>Gets or sets minimum recommended interval.</summary>
    public int MinRecognitionIntervalMs { get; set; } = 500;
    /// <summary>Gets or sets maximum recommended interval.</summary>
    public int MaxRecognitionIntervalMs { get; set; } = 5000;
    /// <summary>Gets or sets required stable preview frames.</summary>
    public int RequiredStableFrames { get; set; } = 2;
    /// <summary>Gets or sets cooldown after recognition.</summary>
    public int PostRecognitionCooldownMs { get; set; } = 1200;
    /// <summary>Gets or sets whether busy frames are dropped.</summary>
    public bool DropFrameWhenBusy { get; set; } = true;
    /// <summary>Gets or sets process priority.</summary>
    public string ProcessPriority { get; set; } = "BelowNormal";
    /// <summary>Gets or sets CPU thread count.</summary>
    public int CpuThreads { get; set; } = 2;
}

/// <summary>Download state exposed to the UI.</summary>
public sealed record QwenDownloadState(bool IsDownloading, double? Progress, string Status);

/// <summary>A normalized character occurrence.</summary>
public sealed record SmartBpNormalizedCharacter(string? RawCharacterName, string? ResolvedCharacterKey,
    string? ResolvedCharacterName, Camp Camp, int SlotIndex, double Confidence, IReadOnlyList<string> Warnings);

/// <summary>Recognition preview returned to the UI.</summary>
public sealed record SmartBpRecognitionPreview(string RawResponse, string NormalizedSummary, long ElapsedMilliseconds,
    int RecommendedIntervalMilliseconds, string? Error);

/// <summary>Built-in recognition sample.</summary>
public sealed record SmartBpTestFrame(string Id, string FileName, SmartBpRecognitionTask Task);

/// <summary>One timestamped AI pipeline diagnostic message.</summary>
public sealed class SmartBpDebugMessageEventArgs : EventArgs
{
    /// <summary>Initializes a diagnostic message.</summary>
    /// <param name="timestamp">Message timestamp.</param>
    /// <param name="source">Subsystem name.</param>
    /// <param name="message">Message text.</param>
    public SmartBpDebugMessageEventArgs(DateTimeOffset timestamp, string source, string message)
    {
        Timestamp = timestamp;
        Source = source;
        Message = message;
    }
    /// <summary>Gets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; }
    /// <summary>Gets the subsystem name.</summary>
    public string Source { get; }
    /// <summary>Gets the message text.</summary>
    public string Message { get; }
}
