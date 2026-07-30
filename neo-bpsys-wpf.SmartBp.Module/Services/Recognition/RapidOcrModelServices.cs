using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 从 SmartBP 模块资源读取 RapidOCR 模型 manifest。
/// </summary>
internal sealed class RapidOcrModelManifestProvider(
    ISmartBpModuleStorageProvider storage,
    ILogger<RapidOcrModelManifestProvider> logger) : IRapidOcrModelManifestProvider
{
    /// <summary>
    /// 加载并校验 RapidOCR 模型 manifest。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>模型 manifest。</returns>
    public async Task<RapidOcrModelManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(storage.ModuleRoot, "Resources", "SmartBp", "RapidOcrModelManifest.json");
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<RapidOcrModelManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("RapidOCR model manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Models.Count == 0 ||
            manifest.Models.Any(profile => string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Version) ||
                GetAssets(profile).Any(asset => string.IsNullOrWhiteSpace(asset.FileName) ||
                    !Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out _))))
            throw new InvalidDataException("Unsupported or invalid RapidOCR model manifest.");
        logger.LogInformation("RapidOCR manifest loaded. Profiles={Count}", manifest.Models.Count);
        return manifest;
    }

    /// <summary>
    /// 枚举一个 RapidOCR profile 的全部资产。
    /// </summary>
    private static IEnumerable<RapidOcrModelAsset> GetAssets(RapidOcrModelProfile profile)
    {
        yield return profile.Det;
        yield return profile.Cls;
        yield return profile.Rec;
        yield return profile.Dict;
    }
}

/// <summary>
/// 管理 RapidOCR 模型 profile 的安装、删除和更新检查。
/// </summary>
internal sealed class RapidOcrModelAssetManager(
    IRapidOcrModelManifestProvider manifestProvider,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpModuleStorageProvider storage,
    ILogger<RapidOcrModelAssetManager> logger,
    IFileDownloadService fileDownloadService) : IRapidOcrModelAssetManager, IDisposable
{
    private const string InstallManifestFileName = ".smartbp-install.json";
    private const string OfficialManifestUrl =
        "https://raw.githubusercontent.com/RapidAI/RapidOCR/refs/heads/main/python/rapidocr/default_models.yaml";
    private const string DownloadFailure = "RapidOCR model download failed. Please verify the official ModelScope URL and file integrity.";
    private CancellationTokenSource? _downloadCts;
    private IFileDownloadOperation? _currentDownload;

    /// <summary>
    /// 模型下载或安装状态变化事件。
    /// </summary>
    public event EventHandler<SmartBpDownloadState>? StateChanged;

    /// <summary>
    /// 最近一次查询得到的 RapidOCR 模型状态。
    /// </summary>
    public RapidOcrModelStatus Status { get; private set; } = new("", "", false, []);

    /// <summary>
    /// 获取 manifest 中可用的 RapidOCR 模型 profile。
    /// </summary>
    public async Task<IReadOnlyList<RapidOcrModelProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default) =>
        (await manifestProvider.LoadAsync(cancellationToken).ConfigureAwait(false)).Models;

    public async Task<RapidOcrModelUpdateCheckResult> CheckForUpdatesAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(storage.RapidOcrModelsRoot);
        var temporaryPath = Path.Combine(storage.RapidOcrModelsRoot, $".official-manifest-{Guid.NewGuid():N}.yaml");
        try
        {
            await SmartBpParallelDownload.DownloadFileAsync(
                fileDownloadService,
                OfficialManifestUrl,
                temporaryPath,
                cancellationToken).ConfigureAwait(false);
            var yaml = await File.ReadAllTextAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
            var officialVersion = ExtractOfficialVersion(yaml, profile.Rec.DownloadUrl)
                ?? throw new InvalidDataException($"RapidOCR official manifest does not contain {profile.Rec.FileName}.");
            var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
            var bundledCurrent = string.Equals(profile.Version, officialVersion, StringComparison.OrdinalIgnoreCase);
            return new(status.InstalledVersion, profile.Version, officialVersion,
                status.HasUpdate && bundledCurrent, bundledCurrent);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<RapidOcrModelStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(settings.Settings.SelectedRapidOcrModelId, cancellationToken).ConfigureAwait(false);
        var paths = GetPaths(profile);
        var missing = GetAssets(profile)
            .Where(item => !File.Exists(Path.Combine(paths.Directory, Path.GetFileName(item.Asset.FileName))))
            .Select(item => item.Asset.FileName)
            .ToArray();
        var isInstalled = missing.Length == 0;
        var installed = isInstalled
            ? await ReadInstallManifestAsync(paths.Directory, cancellationToken).ConfigureAwait(false)
            : null;
        var fingerprint = ComputeProfileFingerprint(profile);
        var hasUpdate = isInstalled && (installed == null ||
            !string.Equals(installed.ProfileId, profile.Id, StringComparison.Ordinal) ||
            !string.Equals(installed.Version, profile.Version, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(installed.ManifestFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));
        Status = new(profile.Id, paths.Directory, isInstalled, missing, false,
            installed?.Version, profile.Version, hasUpdate);
        return Status;
    }

    public async Task InstallAsync(string profileId, CancellationToken cancellationToken = default)
    {
        if (_downloadCts != null) throw new InvalidOperationException("A RapidOCR model download is already active.");
        var profile = await GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var finalRoot = GetPaths(profile).Directory;
        var temporaryRoot = finalRoot + ".install-download";
        try
        {
            Directory.CreateDirectory(temporaryRoot);
            var assets = GetAssets(profile).ToArray();
            for (var index = 0; index < assets.Length; index++)
            {
                var item = assets[index];
                var from = index * 100D / assets.Length;
                var to = (index + 1) * 100D / assets.Length;
                await DownloadAssetAsync(item.Asset, temporaryRoot, from, to, _downloadCts.Token).ConfigureAwait(false);
            }

            var installManifest = new RapidOcrInstallManifest(
                profile.Id,
                profile.Version,
                ComputeProfileFingerprint(profile),
                DateTimeOffset.UtcNow);
            await File.WriteAllTextAsync(
                Path.Combine(temporaryRoot, InstallManifestFileName),
                JsonSerializer.Serialize(installManifest, new JsonSerializerOptions { WriteIndented = true }),
                _downloadCts.Token).ConfigureAwait(false);

            Directory.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
            if (Directory.Exists(finalRoot)) Directory.Delete(finalRoot, true);
            Directory.Move(temporaryRoot, finalRoot);
            settings.Settings.SelectedRapidOcrModelId = profile.Id;
            await settings.SaveAsync(_downloadCts.Token).ConfigureAwait(false);
            await GetStatusAsync(_downloadCts.Token).ConfigureAwait(false);
            Raise(new(false, 100, "SmartBpOcrStatusInstalled"));
            logger.LogInformation("RapidOCR profile installed. Profile={ProfileId}, Directory={Directory}", profile.Id, finalRoot);
        }
        catch (OperationCanceledException)
        {
            Raise(new(false, null, "SmartBpDownloadCancelled"));
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RapidOCR profile installation failed. Profile={ProfileId}", profile.Id);
            Raise(new(false, null, "SmartBpDownloadFailedSimple", ErrorMessage: $"{DownloadFailure} {ex.Message}"));
            throw new InvalidOperationException(DownloadFailure, ex);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)
                && !Directory.EnumerateFiles(temporaryRoot, "*.download.part", SearchOption.AllDirectories).Any())
                Directory.Delete(temporaryRoot, true);
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    public async Task DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        if (_downloadCts != null) throw new InvalidOperationException("Cannot delete RapidOCR models while downloading.");
        var profile = await GetProfileAsync(profileId, cancellationToken).ConfigureAwait(false);
        var directory = GetPaths(profile).Directory;
        await Task.Run(() => { if (Directory.Exists(directory)) Directory.Delete(directory, true); }, cancellationToken).ConfigureAwait(false);
        await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        Raise(new(false, null, "SmartBpOcrStatusMissing"));
    }

    public void Cancel() => _downloadCts?.Cancel();

    public void Pause() => _currentDownload?.Pause();

    public void Resume() => _currentDownload?.Resume();

    public async Task<RapidOcrInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsInstalled) throw new FileNotFoundException($"Managed RapidOCR Chinese model is missing: {string.Join(", ", status.MissingFiles)}");
        var profile = await GetProfileAsync(status.ProfileId, cancellationToken).ConfigureAwait(false);
        return GetPaths(profile);
    }

    /// <summary>
    /// 按 profile ID 读取 RapidOCR 模型 profile。
    /// </summary>
    private async Task<RapidOcrModelProfile> GetProfileAsync(string profileId, CancellationToken cancellationToken)
    {
        var profiles = await GetAvailableProfilesAsync(cancellationToken).ConfigureAwait(false);
        return profiles.SingleOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"RapidOCR profile '{profileId}' is not present in the manifest.");
    }

    /// <summary>
    /// 计算某个 RapidOCR profile 的安装路径集合。
    /// </summary>
    private RapidOcrInstalledPaths GetPaths(RapidOcrModelProfile profile)
    {
        var directory = Path.Combine(storage.RapidOcrModelsRoot, profile.Id);
        return new(profile.Id, directory,
            Path.Combine(directory, Path.GetFileName(profile.Det.FileName)),
            Path.Combine(directory, Path.GetFileName(profile.Cls.FileName)),
            Path.Combine(directory, Path.GetFileName(profile.Rec.FileName)),
            Path.Combine(directory, Path.GetFileName(profile.Dict.FileName)));
    }

    /// <summary>
    /// 按 det/cls/rec/dict 顺序枚举 profile 资产。
    /// </summary>
    private static IEnumerable<(string Name, RapidOcrModelAsset Asset)> GetAssets(RapidOcrModelProfile profile)
    {
        yield return ("det", profile.Det);
        yield return ("cls", profile.Cls);
        yield return ("rec", profile.Rec);
        yield return ("dict", profile.Dict);
    }

    /// <summary>
    /// 下载、校验并按需转换单个 RapidOCR 资产。
    /// </summary>
    private async Task DownloadAssetAsync(RapidOcrModelAsset asset,
        string targetRoot, double from, double to, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out _))
            throw new InvalidDataException($"RapidOCR asset '{asset.FileName}' has no valid official download URL.");
        var downloadedPath = Path.Combine(targetRoot, Path.GetFileName(asset.FileName) + ".download");
        await SmartBpParallelDownload.DownloadFileAsync(
            fileDownloadService,
            asset.DownloadUrl,
            downloadedPath,
            cancellationToken,
            progress =>
            {
                var overall = from + (to - from) * (progress.Percentage ?? 0) / 100D;
                var length = progress.TotalBytes;
                TimeSpan? eta = length is > 0 && progress.BytesPerSecond > 1
                    ? TimeSpan.FromSeconds(Math.Max(0, length.Value - progress.BytesReceived) / progress.BytesPerSecond)
                    : null;
                Raise(new(true, overall, "SmartBpRapidOcrDownloading", asset.FileName,
                    progress.BytesReceived, length, progress.BytesPerSecond, eta,
                    IsPaused: _currentDownload?.State == FileDownloadState.Paused));
            },
            operation => _currentDownload = operation).ConfigureAwait(false);
        var sourceBytes = await File.ReadAllBytesAsync(downloadedPath, cancellationToken).ConfigureAwait(false);
        ValidateSha256(sourceBytes, asset.Sha256, asset.RemotePath);
        var installedBytes = string.Equals(asset.Transform, "PaddleCharacterDictionaryYaml", StringComparison.OrdinalIgnoreCase)
            ? ExtractPaddleCharacterDictionary(sourceBytes)
            : string.Equals(asset.Transform, "Direct", StringComparison.OrdinalIgnoreCase)
                ? sourceBytes
                : throw new InvalidDataException($"Unsupported RapidOCR asset transform '{asset.Transform}'.");
        var path = Path.Combine(targetRoot, Path.GetFileName(asset.FileName));
        await File.WriteAllBytesAsync(path, installedBytes, cancellationToken).ConfigureAwait(false);
        File.Delete(downloadedPath);
    }

    /// <summary>
    /// 从 PaddleOCR YAML 元数据中抽取纯字符字典文件内容。
    /// </summary>
    internal static byte[] ExtractPaddleCharacterDictionary(byte[] yamlBytes)
    {
        var lines = Encoding.UTF8.GetString(yamlBytes).Replace("\r\n", "\n").Split('\n');
        var marker = Array.FindIndex(lines, line => line.Trim() == "character_dict:");
        if (marker < 0) throw new InvalidDataException("Paddle recognition metadata has no character_dict.");
        var characters = new List<string>();
        for (var index = marker + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (!line.StartsWith("  - ", StringComparison.Ordinal)) break;
            characters.Add(line[4..]);
        }
        if (characters.Count < 100) throw new InvalidDataException("Paddle recognition character_dict is incomplete.");
        return new UTF8Encoding(false).GetBytes(string.Join('\n', characters) + "\n");
    }

    private static void ValidateSha256(byte[] bytes, string? expected, string path)
    {
        if (string.IsNullOrWhiteSpace(expected)) return;
        var actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA256 validation failed for {path}.");
    }

    internal static string ComputeProfileFingerprint(RapidOcrModelProfile profile)
    {
        var value = string.Join('\n', GetAssets(profile).Select(item =>
            $"{item.Name}|{item.Asset.FileName}|{item.Asset.DownloadUrl}|{item.Asset.Sha256}|{item.Asset.Transform}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    internal static string? ExtractOfficialVersion(string yaml, string recognitionModelUrl)
    {
        if (!Uri.TryCreate(recognitionModelUrl, UriKind.Absolute, out var configuredUri)) return null;
        var fileName = Path.GetFileName(configuredUri.AbsolutePath);
        foreach (var rawLine in yaml.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("model_dir:", StringComparison.Ordinal) ||
                !Uri.TryCreate(line["model_dir:".Length..].Trim(), UriKind.Absolute, out var uri) ||
                !uri.AbsolutePath.Contains("/onnx/", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(uri.AbsolutePath), fileName, StringComparison.OrdinalIgnoreCase))
                continue;
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var resolveIndex = Array.FindIndex(segments, segment =>
                string.Equals(segment, "resolve", StringComparison.OrdinalIgnoreCase));
            if (resolveIndex >= 0 && resolveIndex + 1 < segments.Length)
                return Uri.UnescapeDataString(segments[resolveIndex + 1]);
        }
        return null;
    }

    private static async Task<RapidOcrInstallManifest?> ReadInstallManifestAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, InstallManifestFileName);
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<RapidOcrInstallManifest>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Raise(SmartBpDownloadState state) => StateChanged?.Invoke(this, state);

    public void Dispose()
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
    }

    private sealed record RapidOcrInstallManifest(
        string ProfileId,
        string Version,
        string ManifestFingerprint,
        DateTimeOffset InstalledAt);
}
