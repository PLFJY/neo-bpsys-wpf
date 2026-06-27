using System.Net.Http;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Globalization;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 计算模型下载速度并限制进度事件的触发频率。
/// </summary>
internal sealed class SmartBpDownloadProgressTracker
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly TimeSpan _throttle = TimeSpan.FromMilliseconds(250);
    private TimeSpan _lastRaised = TimeSpan.MinValue;

    /// <summary>
    /// 判断当前下载进度是否应该通知 UI，并输出速度和剩余时间估计。
    /// </summary>
    /// <param name="bytesReceived">已接收字节数。</param>
    /// <param name="totalBytes">总字节数；未知时为 <see langword="null"/>。</param>
    /// <param name="bytesPerSecond">当前估算下载速度。</param>
    /// <param name="eta">当前估算剩余时间；未知时为 <see langword="null"/>。</param>
    /// <returns>应通知 UI 返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool ShouldRaise(long bytesReceived, long? totalBytes, out double bytesPerSecond, out TimeSpan? eta)
    {
        var elapsed = Math.Max(0.001, _watch.Elapsed.TotalSeconds);
        bytesPerSecond = bytesReceived / elapsed;
        eta = totalBytes is > 0 && bytesPerSecond > 1
            ? TimeSpan.FromSeconds(Math.Max(0, (totalBytes.Value - bytesReceived) / bytesPerSecond))
            : null;
        if (_lastRaised != TimeSpan.MinValue && _watch.Elapsed - _lastRaised < _throttle && bytesReceived != totalBytes)
            return false;
        _lastRaised = _watch.Elapsed;
        return true;
    }
}

/// <summary>
/// 从模块资源加载本地视觉模型 manifest，并兼容旧 Qwen 专用接口。
/// </summary>
internal sealed class QwenModelManifestProvider : IQwenModelManifestProvider, ILocalVisionModelManifestProvider
{
    private readonly ISmartBpModuleStorageProvider? _storage;
    private readonly ILogger<QwenModelManifestProvider> _logger;

    /// <summary>
    /// 初始化从应用基目录读取资源的 Qwen manifest Provider。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public QwenModelManifestProvider(ILogger<QwenModelManifestProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化从 SmartBP 模块目录读取资源的 Qwen manifest Provider。
    /// </summary>
    /// <param name="storage">SmartBP 模块存储Provider。</param>
    /// <param name="logger">日志记录器。</param>
    public QwenModelManifestProvider(
        ISmartBpModuleStorageProvider storage,
        ILogger<QwenModelManifestProvider> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QwenModelManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_storage?.ModuleRoot ?? AppContext.BaseDirectory, "Resources", "SmartBp", "QwenModelManifest.json");
        await using var stream = File.OpenRead(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        var manifest = await JsonSerializer.DeserializeAsync<QwenModelManifest>(stream,
            options, cancellationToken)
            ?? throw new InvalidDataException("Qwen model manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Models.Count == 0) throw new InvalidDataException("Unsupported Qwen model manifest.");
        _logger.LogInformation("Qwen manifest loaded. Profiles={Count}", manifest.Models.Count);
        return manifest;
    }

    async Task<LocalVisionModelManifest> ILocalVisionModelManifestProvider.LoadAsync(CancellationToken cancellationToken)
    {
        var manifest = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return new LocalVisionModelManifest
        {
            SchemaVersion = manifest.SchemaVersion,
            Models = manifest.Models.Select(ToLocalVisionProfile).ToList()
        };
    }

    private static LocalVisionModelProfile ToLocalVisionProfile(QwenModelProfile profile) => new()
    {
        Id = profile.Id,
        DisplayName = profile.DisplayName,
        Family = profile.Family,
        Role = profile.Role,
        SourceType = profile.SourceType,
        ModelUrl = profile.ModelUrl,
        ModelFileName = profile.ModelFileName,
        MmprojUrl = profile.MmprojUrl,
        MmprojFileName = profile.MmprojFileName,
        HuggingFaceRepoId = profile.HuggingFaceRepoId,
        HuggingFaceRevision = profile.HuggingFaceRevision,
        ProjectorMode = profile.ProjectorMode,
        Sha256 = profile.Sha256,
        MmprojSha256 = profile.MmprojSha256,
        Recommended = profile.Recommended,
        Experimental = profile.Experimental,
        DefaultStructuredOutputMode = profile.DefaultStructuredOutputMode
    };
}

/// <summary>
/// 读取、规范化并持久化 SmartBP 识别设置。
/// </summary>
internal sealed class SmartBpRecognitionSettingsService : ISmartBpRecognitionSettingsService
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly string _path = Path.Combine(AppConstants.AppDataPath, "SmartBp", "RecognitionSettings.json");
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    /// <inheritdoc />
    public SmartBpRecognitionSettings Settings { get; private set; }

    /// <summary>
    /// 初始化识别设置服务，并迁移旧设置字段到当前字段。
    /// </summary>
    public SmartBpRecognitionSettingsService()
    {
        var hasRecognitionStrategy = false;
        var hasSelectedBusinessAiModelId = false;
        var hasSelectedOcrProviderMode = false;
        var hasBusinessAiServerPort = false;
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    hasRecognitionStrategy = document.RootElement.TryGetProperty("recognitionStrategy", out _);
                    hasSelectedBusinessAiModelId = document.RootElement.TryGetProperty("selectedBusinessAiModelId", out _);
                    hasSelectedOcrProviderMode = document.RootElement.TryGetProperty("selectedOcrProviderMode", out _);
                    hasBusinessAiServerPort = document.RootElement.TryGetProperty("businessAiServerPort", out _);
                }
                Settings = JsonSerializer.Deserialize<SmartBpRecognitionSettings>(NormalizeOcrOnlySettingsJson(json), Options) ?? new();
            }
            else
            {
                Settings = new();
            }
        }
        catch { Settings = new(); }
        Settings.RecognitionStrategy = SmartBpRecognitionStrategy.PureOcr;
        if (!hasSelectedBusinessAiModelId)
            Settings.SelectedBusinessAiModelId = Settings.SelectedQwenModelId;
        if (!hasSelectedOcrProviderMode)
            Settings.SelectedOcrProviderMode = Settings.OcrProviderMode;
        if (!hasBusinessAiServerPort)
            Settings.BusinessAiServerPort = Settings.LlamaServerPort;
        if (string.IsNullOrWhiteSpace(Settings.SelectedBusinessAiModelId))
            Settings.SelectedBusinessAiModelId = string.IsNullOrWhiteSpace(Settings.SelectedQwenModelId) ? "qwen3.5-2b-q4km" : Settings.SelectedQwenModelId;
        if (string.IsNullOrWhiteSpace(Settings.SelectedAiOcrModelId))
            Settings.SelectedAiOcrModelId = "paddleocr-vl-1.6-gguf";
        Settings.SelectedQwenModelId = Settings.SelectedBusinessAiModelId;
        Settings.OcrProviderMode = Settings.SelectedOcrProviderMode;
        Settings.RecognitionEngine = SmartBpRecognitionEngine.Ocr;
        Settings.LlamaServerPort = Settings.BusinessAiServerPort;
        Settings.LlamaServerPort = Math.Clamp(Settings.LlamaServerPort, 1024, 65535);
        Settings.BusinessAiServerPort = Math.Clamp(Settings.BusinessAiServerPort, 1024, 65535);
        Settings.AiOcrServerPort = Math.Clamp(Settings.AiOcrServerPort, 1024, 65535);
        Settings.AiRequestTimeoutSeconds = Math.Clamp(Settings.AiRequestTimeoutSeconds, 5, 300);
        Settings.AiStartupTimeoutSeconds = Math.Clamp(Settings.AiStartupTimeoutSeconds, 15, 600);
        Settings.LlamaContextSize = Math.Clamp(Settings.LlamaContextSize, 8192, 32768);
        Settings.MaxImageWidth = Math.Clamp(Settings.MaxImageWidth, 320, 4096);
        Settings.RecognitionIntervalMs = Math.Clamp(Settings.RecognitionIntervalMs, 500, 5000);
        Settings.CpuThreads = Math.Clamp(Settings.CpuThreads, 1, 64);
        Settings.FocusedMaxTokens = Math.Clamp(Settings.FocusedMaxTokens, 1024, 4096);
        Settings.FullScanMaxTokens = Math.Clamp(Settings.FullScanMaxTokens, 2048, 8192);
        Settings.StageConfidenceThreshold = Math.Clamp(Settings.StageConfidenceThreshold, 0, 1);
        Settings.GuidanceSyncLookAheadSteps = Math.Clamp(Settings.GuidanceSyncLookAheadSteps, 1, 20);
        Settings.RequiredStableSnapshots = Math.Clamp(Settings.RequiredStableSnapshots, 1, 5);
        Settings.OcrRecognitionIntervalMs = Math.Clamp(Settings.OcrRecognitionIntervalMs, 100, 5000);
        Settings.MinimumOcrRecognitionIntervalMs = Math.Clamp(Settings.MinimumOcrRecognitionIntervalMs, 0, 300000);
        Settings.MinimumAiRecognitionIntervalMs = Math.Clamp(Settings.MinimumAiRecognitionIntervalMs, 0, 300000);
        Settings.AiUnknownPhaseTalentInferenceFrames = Math.Clamp(Settings.AiUnknownPhaseTalentInferenceFrames, 1, 30);
        Settings.OcrFieldStaleMilliseconds = Math.Clamp(Settings.OcrFieldStaleMilliseconds, 250, 30000);
        Settings.OcrBackfillLookBehindSteps = Math.Clamp(Settings.OcrBackfillLookBehindSteps, 0, 20);
        Settings.TesseractDefaultPsm = Math.Clamp(Settings.TesseractDefaultPsm, 0, 13);
        Settings.TesseractMaxPreprocessVariants = Math.Clamp(Settings.TesseractMaxPreprocessVariants, 1, 3);
        Settings.TesseractLanguages = string.IsNullOrWhiteSpace(Settings.TesseractLanguages)
            ? "chi_sim+eng"
            : Settings.TesseractLanguages.Trim();
        Settings.SelectedRapidOcrModelId = string.IsNullOrWhiteSpace(Settings.SelectedRapidOcrModelId)
            ? "ppocr-v5-zh-mobile"
            : Settings.SelectedRapidOcrModelId.Trim();
        Settings.RapidOcrPadding = Math.Clamp(Settings.RapidOcrPadding, 0, 256);
        Settings.RapidOcrMaxSideLen = Math.Clamp(Settings.RapidOcrMaxSideLen, 320, 4096);
        Settings.RapidOcrBoxScoreThreshold = Math.Clamp(Settings.RapidOcrBoxScoreThreshold, 0, 1);
        Settings.RapidOcrBoxThreshold = Math.Clamp(Settings.RapidOcrBoxThreshold, 0, 1);
        Settings.RapidOcrUnclipRatio = Math.Clamp(Settings.RapidOcrUnclipRatio, 0.1, 5);
        Settings.RecognitionBackfillLookBehindSteps = Math.Clamp(Settings.RecognitionBackfillLookBehindSteps, 0, 20);
        Settings.RecognitionFieldStaleMilliseconds = Math.Clamp(Settings.RecognitionFieldStaleMilliseconds, 250, 30000);
        Settings.RecognitionVisualBufferMilliseconds = Math.Clamp(Settings.RecognitionVisualBufferMilliseconds, 0, 5000);
        Settings.LlamaParallelSlots = Math.Clamp(Settings.LlamaParallelSlots, 1, 8);
        Settings.LlamaGpuLayers = Math.Clamp(Settings.LlamaGpuLayers, -1, 999);
        Settings.LlamaBatchSize = Math.Clamp(Settings.LlamaBatchSize, 32, 4096);
        Settings.LlamaUBatchSize = Math.Clamp(Settings.LlamaUBatchSize, 32, 4096);
        Settings.PhaseCropMaxImageWidth = Math.Clamp(Settings.PhaseCropMaxImageWidth, 320, Settings.MaxImageWidth);
        Settings.ContentCropMaxImageWidth = Math.Clamp(Settings.ContentCropMaxImageWidth, 320, Settings.MaxImageWidth);
        Settings.PhaseMaxTokens = Math.Clamp(Settings.PhaseMaxTokens, 16, 256);
        Settings.SnapshotDeltaMaxTokens = Math.Clamp(Settings.SnapshotDeltaMaxTokens, 128, 4096);
        Settings.BannedSurFieldMaxTokens = Math.Clamp(Settings.BannedSurFieldMaxTokens, 64, 2048);
        Settings.BannedHunFieldMaxTokens = Math.Clamp(Settings.BannedHunFieldMaxTokens, 64, 2048);
        Settings.PickedSurFieldMaxTokens = Math.Clamp(Settings.PickedSurFieldMaxTokens, 64, 2048);
        Settings.PickedHunFieldMaxTokens = Math.Clamp(Settings.PickedHunFieldMaxTokens, 64, 2048);
        Settings.PhaseTransitionCommitHoldMilliseconds = Math.Clamp(Settings.PhaseTransitionCommitHoldMilliseconds, 0, 2000);
        Settings.PhaseTransitionCommitHoldMaxMilliseconds = Math.Clamp(Settings.PhaseTransitionCommitHoldMaxMilliseconds, Settings.PhaseTransitionCommitHoldMilliseconds, 3000);
        Settings.RecognitionFrameBufferMilliseconds = Math.Clamp(Settings.RecognitionFrameBufferMilliseconds, 250, 5000);
        Settings.RecognitionTransitionLookBehindMilliseconds = Math.Clamp(Settings.RecognitionTransitionLookBehindMilliseconds, 100, 5000);
        Settings.RecognitionCropChangeThreshold = Math.Clamp(Settings.RecognitionCropChangeThreshold, 0.001, 1);
        Settings.RecognitionCropStableFrames = Math.Clamp(Settings.RecognitionCropStableFrames, 1, 10);
        Settings.LlamaRuntimeUpdateCheckIntervalHours = Math.Clamp(Settings.LlamaRuntimeUpdateCheckIntervalHours, 1, 24 * 30);
        if (string.IsNullOrWhiteSpace(Settings.PromptProfileId)) Settings.PromptProfileId = "zh-CN";
        if (Settings.SelectedQwenModelId == "qwen3.5-2b-q4ks") Settings.SelectedQwenModelId = "qwen3.5-2b-q4km";
    }

    private static string NormalizeOcrOnlySettingsJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json) as JsonObject;
            if (node == null) return json;
            SetCaseInsensitive(node, "recognitionStrategy", 0);
            SetCaseInsensitive(node, "recognitionEngine", 0);
            return node.ToJsonString();
        }
        catch
        {
            return json;
        }
    }

    private static void SetCaseInsensitive(JsonObject node, string propertyName, JsonNode? value)
    {
        var existing = node
            .Select(pair => pair.Key)
            .FirstOrDefault(key => string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase));
        node[existing ?? propertyName] = value;
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(Settings, Options), cancellationToken);
            File.Move(temporary, _path, true);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}

/// <summary>
/// 管理本地视觉模型的安装、完整性校验、删除和下载状态通知。
/// </summary>
internal sealed class QwenModelAssetManager(
    IQwenModelManifestProvider manifestProvider,
    ISmartBpRecognitionSettingsService settingsService,
    ISmartBpModuleStorageProvider storage,
    ILogger<QwenModelAssetManager> logger) : IQwenModelAssetManager, ILocalVisionModelAssetManager
{
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private CancellationTokenSource? _downloadCts;
    public event EventHandler<QwenDownloadState>? StateChanged;
    public QwenDownloadState State { get; private set; } = new(false, null, "SmartBpAiStatusNotInstalled");

    public async Task<QwenModelProfile> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await manifestProvider.LoadAsync(cancellationToken);
        var selected = string.IsNullOrWhiteSpace(settingsService.Settings.SelectedBusinessAiModelId)
            ? settingsService.Settings.SelectedQwenModelId
            : settingsService.Settings.SelectedBusinessAiModelId;
        return manifest.Models.SingleOrDefault(x => x.Id == selected)
            ?? throw new InvalidDataException($"Local vision profile '{selected}' is not present in the manifest.");
    }

    public async Task<QwenModelProfile> GetProfileAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var manifest = await manifestProvider.LoadAsync(cancellationToken);
        return manifest.Models.SingleOrDefault(x => x.Id == modelId)
            ?? throw new InvalidDataException($"Local vision profile '{modelId}' is not present in the manifest.");
    }

    public async Task<IReadOnlyList<QwenModelProfile>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        (await manifestProvider.LoadAsync(cancellationToken)).Models;

    public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(cancellationToken); var paths = GetPaths(p);
        var installed = await MatchesAsync(paths.Model, p.Sha256, cancellationToken) &&
            (p.MmprojMode != QwenMmprojMode.Separate ||
             paths.Mmproj != null && await MatchesAsync(paths.Mmproj, p.MmprojSha256, cancellationToken));
        logger.LogInformation("Qwen model install status. Model={ModelId}, Installed={Installed}", p.Id, installed);
        return installed;
    }

    public async Task<bool> IsInstalledAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(modelId, cancellationToken); var paths = GetPaths(p);
        var installed = await MatchesAsync(paths.Model, p.Sha256, cancellationToken) &&
            (p.MmprojMode != QwenMmprojMode.Separate ||
             paths.Mmproj != null && await MatchesAsync(paths.Mmproj, p.MmprojSha256, cancellationToken));
        logger.LogInformation("Qwen model install status. Model={ModelId}, Installed={Installed}", p.Id, installed);
        return installed;
    }

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(cancellationToken).ConfigureAwait(false);
        await InstallAsync(p, cancellationToken).ConfigureAwait(false);
    }

    public async Task InstallAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(modelId, cancellationToken).ConfigureAwait(false);
        await InstallAsync(p, cancellationToken).ConfigureAwait(false);
    }

    private async Task InstallAsync(QwenModelProfile p, CancellationToken cancellationToken)
    {
        if (_downloadCts != null) throw new InvalidOperationException("A Qwen download is already active.");
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var paths = GetPaths(p); Directory.CreateDirectory(paths.Root);
            Set(new(true, 0, "SmartBpAiStatusDownloading")); logger.LogInformation("Qwen model download started. Model={ModelId}", p.Id);
            var modelUrl = ResolveDownloadUrl(p, p.ModelFileName, false, settingsService.Settings, CultureInfo.CurrentUICulture);
            var modelEnd = p.MmprojMode == QwenMmprojMode.Separate ? 50 : 100;
            await DownloadAsync(modelUrl, paths.Model, p.Sha256, 0, modelEnd, _downloadCts.Token);
            if (p.MmprojMode == QwenMmprojMode.Separate)
            {
                if (paths.Mmproj == null || string.IsNullOrWhiteSpace(p.MmprojFileName))
                    throw new InvalidDataException($"Qwen profile '{p.Id}' requires a separate mmproj file.");
                var mmprojUrl = ResolveDownloadUrl(p, p.MmprojFileName, true, settingsService.Settings, CultureInfo.CurrentUICulture);
                await DownloadAsync(mmprojUrl, paths.Mmproj, p.MmprojSha256, 50, 100, _downloadCts.Token);
            }
            Set(new(false, 100, "SmartBpAiStatusInstalled")); logger.LogInformation("Qwen model download completed. Model={ModelId}", p.Id);
        }
        catch (OperationCanceledException) { Set(new(false, null, "SmartBpAiStatusCancelled")); logger.LogInformation("Qwen model download cancelled"); throw; }
        catch (Exception ex) { Set(new(false, null, "SmartBpDownloadFailedSimple", ErrorMessage: ex.ToString())); logger.LogError(ex, "Qwen model download failed"); throw; }
        finally { _downloadCts?.Dispose(); _downloadCts = null; }
    }

    public void Cancel() => _downloadCts?.Cancel();
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(cancellationToken).ConfigureAwait(false);
        await DeleteAsync(p, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(modelId, cancellationToken).ConfigureAwait(false);
        await DeleteAsync(p, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteAsync(QwenModelProfile p, CancellationToken cancellationToken)
    {
        if (_downloadCts != null) throw new InvalidOperationException("Cannot delete while downloading.");
        var root = GetPaths(p).Root;
        await Task.Run(() => { if (Directory.Exists(root)) Directory.Delete(root, true); }, cancellationToken).ConfigureAwait(false);
        Set(new(false, null, "SmartBpAiStatusNotInstalled"));
    }

    public async Task<QwenInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(cancellationToken); var paths = GetPaths(p);
        if (!await IsInstalledAsync(cancellationToken)) throw new FileNotFoundException("The selected Qwen model is not installed.");
        return new(paths.Model, paths.Mmproj, p.MmprojMode);
    }

    public async Task<QwenInstalledPaths> GetInstalledPathsAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(modelId, cancellationToken); var paths = GetPaths(p);
        if (!await IsInstalledAsync(modelId, cancellationToken)) throw new FileNotFoundException($"The selected local vision model '{modelId}' is not installed.");
        return new(paths.Model, paths.Mmproj, p.MmprojMode);
    }

    private (string Root, string Model, string? Mmproj) GetPaths(QwenModelProfile p)
    {
        var root = Path.Combine(storage.QwenModelsRoot, p.Id);
        return (root, Path.Combine(root, Path.GetFileName(p.ModelFileName)),
            p.MmprojMode == QwenMmprojMode.Separate && !string.IsNullOrWhiteSpace(p.MmprojFileName)
                ? Path.Combine(root, Path.GetFileName(p.MmprojFileName))
                : null);
    }

    internal static string ResolveDownloadUrl(
        QwenModelProfile profile,
        string fileName,
        bool isMmproj,
        SmartBpRecognitionSettings settings,
        CultureInfo culture)
    {
        if (profile.SourceType == QwenModelSourceType.DirectUrl)
            return isMmproj
                ? profile.MmprojUrl ?? throw new InvalidDataException($"Qwen profile '{profile.Id}' has no mmproj URL.")
                : !string.IsNullOrWhiteSpace(profile.ModelUrl) ? profile.ModelUrl : throw new InvalidDataException($"Qwen profile '{profile.Id}' has no model URL.");
        if (string.IsNullOrWhiteSpace(profile.HuggingFaceRepoId))
            throw new InvalidDataException($"Qwen profile '{profile.Id}' has no HuggingFace repository id.");
        var endpoint = settings.HuggingFaceEndpointOverride.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            var useMirror = settings.UseHuggingFaceMirrorForChineseUi &&
                profile.UseHuggingFaceMirrorForChineseUi &&
                culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            endpoint = useMirror ? "https://hf-mirror.com" : "https://huggingface.co";
        }
        return $"{endpoint}/{profile.HuggingFaceRepoId.Trim('/')}/resolve/{Uri.EscapeDataString(profile.HuggingFaceRevision)}/{fileName.TrimStart('/')}";
    }

    private async Task DownloadAsync(string url, string finalPath, string? hash, double from, double to, CancellationToken token)
    {
        if (await MatchesAsync(finalPath, hash, token)) return;
        var fileName = Path.GetFileName(finalPath);
        try
        {
            await SmartBpParallelDownload.DownloadFileAsync(
                url,
                finalPath,
                token,
                progress =>
                {
                    var total = progress.TotalBytesToReceive > 0 ? progress.TotalBytesToReceive : (long?)null;
                    var overallProgress = from + (to - from) * progress.ProgressPercentage / 100D;
                    TimeSpan? eta = total is > 0 && progress.BytesPerSecondSpeed > 1
                        ? TimeSpan.FromSeconds(Math.Max(0, total.Value - progress.ReceivedBytesSize) / progress.BytesPerSecondSpeed)
                        : null;
                    Set(new(
                        true,
                        overallProgress,
                        "SmartBpAiStatusDownloading",
                        fileName,
                        progress.ReceivedBytesSize,
                        total,
                        progress.BytesPerSecondSpeed,
                        eta));
                }).ConfigureAwait(false);
            if (!await MatchesAsync(finalPath, hash, token))
                throw new InvalidDataException($"SHA256 validation failed for {Path.GetFileName(finalPath)}.");
        }
        catch
        {
            if (File.Exists(finalPath) && !await MatchesAsync(finalPath, hash, CancellationToken.None))
                File.Delete(finalPath);
            throw;
        }
    }

    internal static void ConfigureDownloadHeaders(HttpClient httpClient, Uri downloadUri)
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
        httpClient.DefaultRequestHeaders.Referrer = new Uri(downloadUri.GetLeftPart(UriPartial.Authority) + "/");
    }

    private static async Task<bool> MatchesAsync(string path, string? expected, CancellationToken token)
    {
        if (!File.Exists(path)) return false; if (string.IsNullOrWhiteSpace(expected)) return true;
        await using var stream = File.OpenRead(path); var hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
    private void Set(QwenDownloadState state) { State = state; StateChanged?.Invoke(this, state); }
}
