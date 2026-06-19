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
    /// <summary>Gets or sets the llama.cpp context size.</summary>
    public int LlamaContextSize { get; set; } = 8192;
    /// <summary>Gets or sets selected Qwen profile.</summary>
    public string SelectedQwenModelId { get; set; } = "qwen3.5-2b-q4km";
    /// <summary>Gets or sets the selected projector profile label.</summary>
    public string SelectedMmprojId { get; set; } = "mmproj-f16";
    /// <summary>Gets or sets the bundled prompt profile id.</summary>
    public string PromptProfileId { get; set; } = "zh-CN";
    /// <summary>Gets or sets the managed llama.cpp runtime asset id.</summary>
    public string SelectedLlamaRuntimeId { get; set; } = "";
    /// <summary>Gets or sets maximum encoded width.</summary>
    public int MaxImageWidth { get; set; } = 1280;
    /// <summary>Gets or sets image encoding format.</summary>
    public string ImageFormat { get; set; } = "png";
    /// <summary>Gets or sets inference temperature.</summary>
    public double Temperature { get; set; }
    /// <summary>Gets or sets focused token limit.</summary>
    public int FocusedMaxTokens { get; set; } = 1024;
    /// <summary>Gets or sets full-scan token limit.</summary>
    public int FullScanMaxTokens { get; set; } = 2048;
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

/// <summary>A bundled recognition prompt profile.</summary>
public sealed record SmartBpPromptProfile(string Id, string DisplayName, string SystemPrompt);

/// <summary>llama.cpp runtime manifest root.</summary>
public sealed class LlamaCppRuntimeManifest
{
    /// <summary>Gets or sets schema version.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>Gets or sets upstream runtime version.</summary>
    public string RuntimeVersion { get; set; } = "";
    /// <summary>Gets or sets release page.</summary>
    public string ReleasePage { get; set; } = "";
    /// <summary>Gets or sets runtime assets.</summary>
    public List<LlamaCppRuntimeAsset> Assets { get; set; } = [];
}

/// <summary>One installable llama.cpp runtime archive.</summary>
public sealed class LlamaCppRuntimeAsset
{
    /// <summary>Gets or sets asset id.</summary>
    public string Id { get; set; } = "";
    /// <summary>Gets or sets display name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gets or sets CPU architecture.</summary>
    public string Architecture { get; set; } = "";
    /// <summary>Gets or sets backend.</summary>
    public string Backend { get; set; } = "";
    /// <summary>Gets or sets archive URL.</summary>
    public string Url { get; set; } = "";
    /// <summary>Gets or sets optional SHA256.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Gets or sets executable filename.</summary>
    public string? EntryExe { get; set; }
    /// <summary>Gets or sets required extra asset ids.</summary>
    public List<string> RequiredExtraAssets { get; set; } = [];
}

/// <summary>Managed llama.cpp runtime installation state.</summary>
public sealed record LlamaCppRuntimeInstallState(bool IsDownloading, double? Progress, string Status);

/// <summary>Visual extraction result returned by the model.</summary>
public sealed class SmartBpVisionExtractionResult
{
    /// <summary>Gets or sets schema version.</summary>
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    /// <summary>Gets or sets scene information.</summary>
    [JsonPropertyName("scene")] public SmartBpVisionScene Scene { get; set; } = new();
    /// <summary>Gets or sets visible teams.</summary>
    [JsonPropertyName("teams")] public List<SmartBpVisionTeam> Teams { get; set; } = [];
    /// <summary>Gets or sets flattened visible characters.</summary>
    [JsonPropertyName("all_characters")] public List<SmartBpVisionCharacter> AllCharacters { get; set; } = [];
    /// <summary>Gets or sets flattened player IDs.</summary>
    [JsonPropertyName("all_player_ids")] public List<SmartBpVisionPlayerId> AllPlayerIds { get; set; } = [];
    /// <summary>Gets or sets recognition warnings.</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>Visual scene metadata.</summary>
public sealed class SmartBpVisionScene
{
    /// <summary>Gets or sets game name.</summary>
    [JsonPropertyName("game")] public string Game { get; set; } = "";
    /// <summary>Gets or sets interface type.</summary>
    [JsonPropertyName("interface_type")] public string InterfaceType { get; set; } = "";
    /// <summary>Gets or sets task.</summary>
    [JsonPropertyName("task")] public string Task { get; set; } = "";
    /// <summary>Gets or sets main status text.</summary>
    [JsonPropertyName("main_status")] public string? MainStatus { get; set; }
    /// <summary>Gets or sets pause status text.</summary>
    [JsonPropertyName("pause_status")] public string? PauseStatus { get; set; }
    /// <summary>Gets or sets pause remaining seconds.</summary>
    [JsonPropertyName("pause_remaining_seconds")] public double? PauseRemainingSeconds { get; set; }
}

/// <summary>One visual team region.</summary>
public sealed class SmartBpVisionTeam
{
    /// <summary>Gets or sets screen side.</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>Gets or sets faction.</summary>
    [JsonPropertyName("faction")] public string Faction { get; set; } = "unknown";
    /// <summary>Gets or sets title text.</summary>
    [JsonPropertyName("title_text")] public string? TitleText { get; set; }
    /// <summary>Gets or sets subtitle text.</summary>
    [JsonPropertyName("subtitle_text")] public string? SubtitleText { get; set; }
    /// <summary>Gets or sets slots.</summary>
    [JsonPropertyName("slots")] public List<SmartBpVisionSlot> Slots { get; set; } = [];
}

/// <summary>One visual slot.</summary>
public sealed class SmartBpVisionSlot
{
    /// <summary>Gets or sets slot index.</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>Gets or sets slot state.</summary>
    [JsonPropertyName("slot_state")] public string SlotState { get; set; } = "unknown";
    /// <summary>Gets or sets raw candidate character name.</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>Gets or sets player ID.</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>Gets or sets banned/unavailable flag.</summary>
    [JsonPropertyName("is_banned_or_unavailable")] public bool IsBannedOrUnavailable { get; set; }
    /// <summary>Gets or sets all visible raw text.</summary>
    [JsonPropertyName("raw_visible_text")] public string? RawVisibleText { get; set; }
    /// <summary>Gets or sets confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>One flattened visual character.</summary>
public sealed class SmartBpVisionCharacter
{
    /// <summary>Gets or sets character name.</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>Gets or sets faction.</summary>
    [JsonPropertyName("faction")] public string Faction { get; set; } = "unknown";
    /// <summary>Gets or sets player ID.</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>Gets or sets side.</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>Gets or sets slot index.</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>Gets or sets state.</summary>
    [JsonPropertyName("slot_state")] public string SlotState { get; set; } = "unknown";
    /// <summary>Gets or sets confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>One flattened visual player ID.</summary>
public sealed class SmartBpVisionPlayerId
{
    /// <summary>Gets or sets player ID.</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>Gets or sets character name.</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>Gets or sets side.</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>Gets or sets slot index.</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>Gets or sets confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>A normalized character occurrence.</summary>
public sealed record SmartBpNormalizedCharacter(string? RawCharacterName, string? ResolvedCharacterKey,
    string? ResolvedCharacterName, Camp Camp, int SlotIndex, double Confidence, IReadOnlyList<string> Warnings);

/// <summary>Recognition preview returned to the UI.</summary>
public sealed record SmartBpRecognitionPreview(string RawResponse, string ParsedVisualSummary,
    string ResolvedCharacterSummary, long ElapsedMilliseconds, int RecommendedIntervalMilliseconds, string? Error);

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
