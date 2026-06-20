using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class LlamaCppRuntimeManifestProvider : ILlamaCppRuntimeManifestProvider
{
    private readonly ISmartBpRecognitionSettingsService? _settings;
    private readonly ISmartBpDebugLog? _debugLog;
    private readonly ISmartBpModuleStorageProvider? _storage;

    public LlamaCppRuntimeManifestProvider()
    {
    }

    public LlamaCppRuntimeManifestProvider(
        ISmartBpRecognitionSettingsService settings,
        ISmartBpDebugLog debugLog,
        ISmartBpModuleStorageProvider storage)
    {
        _settings = settings;
        _debugLog = debugLog;
        _storage = storage;
    }

    public async Task<LlamaCppRuntimeManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_settings != null && !string.IsNullOrWhiteSpace(_settings.Settings.LlamaRuntimeManifestApiUrl))
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                using var response = await http.GetAsync(_settings.Settings.LlamaRuntimeManifestApiUrl, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var manifest = await DeserializeAsync(remote, cancellationToken).ConfigureAwait(false);
                _debugLog?.Write("runtime", $"Loaded remote llama.cpp runtime manifest {manifest.RuntimeVersion}.");
                return manifest;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _debugLog?.Write("runtime", $"Remote runtime manifest check failed; using bundled manifest. {ex.Message}");
            }
        }

        return await LoadBundledAsync(_storage?.ModuleRoot, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<LlamaCppRuntimeManifest> LoadBundledAsync(
        string? moduleRoot = null,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(moduleRoot ?? AppContext.BaseDirectory, "Resources", "SmartBp", "LlamaCppRuntimeManifest.json");
        await using var stream = File.OpenRead(path);
        return await DeserializeAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LlamaCppRuntimeManifest> DeserializeAsync(Stream stream, CancellationToken cancellationToken)
    {
        var manifest = await JsonSerializer.DeserializeAsync<LlamaCppRuntimeManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("llama.cpp runtime manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Assets.Count == 0) throw new InvalidDataException("Unsupported llama.cpp runtime manifest.");
        if (!manifest.ReleasePage.EndsWith('/' + manifest.RuntimeVersion, StringComparison.OrdinalIgnoreCase) ||
            manifest.Assets.Any(x => !x.Url.Contains('/' + manifest.RuntimeVersion + '/', StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("llama.cpp runtime manifest version is inconsistent with its release URLs.");
        return manifest;
    }
}

internal sealed class LlamaCppRuntimeUpdateService(
    ISmartBpRecognitionSettingsService settings,
    ISmartBpModuleStorageProvider storage) : ILlamaCppRuntimeUpdateService
{
    private const string GitHubApiUserAgent = "neo-bpsys-wpf/1.0";
    private const string GitHubApiAccept = "application/vnd.github+json";

    /// <inheritdoc />
    public async Task<LlamaCppRuntimeUpdateCheckResult> CheckForUpdatesAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (!settings.Settings.EnableLlamaRuntimeUpdateCheck && !force)
            return new(false, false, "", null, [], "Runtime update checking is disabled.");
        if (!force && settings.Settings.LastLlamaRuntimeUpdateCheckAt is { } last &&
            DateTimeOffset.Now - last < TimeSpan.FromHours(settings.Settings.LlamaRuntimeUpdateCheckIntervalHours))
            return new(false, false, "", null, [], "Runtime update check interval has not elapsed.");

        // Load bundled manifest for asset templates — no network call
        var bundled = await LlamaCppRuntimeManifestProvider.LoadBundledAsync(cancellationToken: cancellationToken);
        var installedVersion = await GetInstalledVersionAsync(cancellationToken).ConfigureAwait(false) ?? "";

        // Fetch latest release tag from GitHub API
        var latestVersion = await FetchLatestReleaseTagAsync(bundled.ReleasePage, cancellationToken).ConfigureAwait(false);
        settings.Settings.LastLlamaRuntimeUpdateCheckAt = DateTimeOffset.Now;
        await settings.SaveAsync(cancellationToken).ConfigureAwait(false);

        if (latestVersion == null)
            return new(true, false, installedVersion, null, [], "Failed to fetch latest llama.cpp release version.");

        var hasUpdate = !string.Equals(installedVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
        var latestAssets = hasUpdate
            ? bundled.Assets.Where(x => !string.IsNullOrWhiteSpace(x.EntryExe))
                .Select(a => CloneAssetWithVersion(a, bundled.RuntimeVersion, latestVersion))
                .ToList()
            : [];

        if (hasUpdate)
        {
            // Persist the updated manifest as a local cache so downloads can use the new URLs
            var cachedManifest = new LlamaCppRuntimeManifest
            {
                SchemaVersion = bundled.SchemaVersion,
                RuntimeVersion = latestVersion,
                ReleasePage = bundled.ReleasePage,
                CheckIntervalHours = bundled.CheckIntervalHours,
                Assets = latestAssets.Concat(
                    bundled.Assets.Where(x => string.IsNullOrWhiteSpace(x.EntryExe)).Select(a =>
                        CloneAssetWithVersion(a, bundled.RuntimeVersion, latestVersion))).ToList()
            };
            await SaveCachedManifestAsync(cachedManifest, cancellationToken).ConfigureAwait(false);
        }

        return new(true, hasUpdate, installedVersion, latestVersion, latestAssets,
            hasUpdate ? $"Latest llama.cpp runtime is {latestVersion}." : "Installed llama.cpp runtime is up to date.");
    }

    /// <summary>Fetches the latest release tag name from the GitHub API.</summary>
    private static async Task<string?> FetchLatestReleaseTagAsync(string releasePageUrl, CancellationToken cancellationToken)
    {
        try
        {
            var repo = ExtractGitHubRepo(releasePageUrl);
            if (repo == null) return null;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(GitHubApiUserAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd(GitHubApiAccept);

            using var response = await http.GetAsync(
                $"https://api.github.com/repos/{repo}/releases/latest", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return document.RootElement.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Extracts "owner/repo" from a GitHub release page URL.</summary>
    internal static string? ExtractGitHubRepo(string releasePageUrl)
    {
        if (string.IsNullOrWhiteSpace(releasePageUrl)) return null;
        var uri = new Uri(releasePageUrl);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? $"{segments[0]}/{segments[1]}" : null;
    }

    /// <summary>Creates a copy of an asset with URLs updated to a new version string.</summary>
    internal static LlamaCppRuntimeAsset CloneAssetWithVersion(LlamaCppRuntimeAsset source, string oldVersion, string newVersion)
    {
        return new LlamaCppRuntimeAsset
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Architecture = source.Architecture,
            Backend = source.Backend,
            Url = source.Url.Replace($"/{oldVersion}/", $"/{newVersion}/"),
            Sha256 = null, // SHA256 changes per release; null means skip verification for updated version
            EntryExe = source.EntryExe,
            RequiredExtraAssets = [.. source.RequiredExtraAssets],
            UrlIsDirectDownload = source.UrlIsDirectDownload
        };
    }

    /// <summary>Saves an updated manifest as a local cache file for download use.</summary>
    private async Task SaveCachedManifestAsync(LlamaCppRuntimeManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storage.LlamaCppRoot);
        var path = Path.Combine(storage.LlamaCppRoot, "LlamaCppRuntimeManifestCache.json");
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }

    private async Task<string?> GetInstalledVersionAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(settings.Settings.SelectedLlamaRuntimeId)) return null;
        var path = Path.Combine(storage.LlamaCppRoot, "Runtimes", settings.Settings.SelectedLlamaRuntimeId, "current", "manifest.json");
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
        return document.RootElement.TryGetProperty("RuntimeVersion", out var pascal)
            ? pascal.GetString()
            : document.RootElement.TryGetProperty("runtimeVersion", out var camel) ? camel.GetString() : null;
    }
}

internal sealed class LlamaCppRuntimeAssetManager(
    ISmartBpRecognitionSettingsService settings,
    ISmartBpModuleStorageProvider storage,
    IGitHubDownloadUrlResolver urlResolver,
    ISmartBpDebugLog debugLog,
    ILogger<LlamaCppRuntimeAssetManager> logger) : ILlamaCppRuntimeAssetManager
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36";
    private CancellationTokenSource? _downloadCts;
    private LlamaCppRuntimeManifest? _bundledManifest;
    public event EventHandler<LlamaCppRuntimeInstallState>? StateChanged;
    public LlamaCppRuntimeInstallState State { get; private set; } = new(false, null, "SmartBpAiStatusNotInstalled");

    /// <summary>Gets the bundled manifest, loaded and cached once.</summary>
    private async Task<LlamaCppRuntimeManifest> GetBundledManifestAsync(CancellationToken cancellationToken = default)
    {
        if (_bundledManifest != null) return _bundledManifest;
        _bundledManifest = await LlamaCppRuntimeManifestProvider.LoadBundledAsync(cancellationToken: cancellationToken);
        return _bundledManifest;
    }

    /// <summary>Loads the manifest to use for available assets. Prefers the locally cached update manifest if it exists and is newer than the bundled version.</summary>
    private async Task<LlamaCppRuntimeManifest> GetEffectiveManifestAsync(CancellationToken cancellationToken = default)
    {
        var bundled = await GetBundledManifestAsync(cancellationToken);
        var cachePath = Path.Combine(storage.LlamaCppRoot, "LlamaCppRuntimeManifestCache.json");
        if (!File.Exists(cachePath)) return bundled;
        try
        {
            await using var stream = File.OpenRead(cachePath);
            var cached = await JsonSerializer.DeserializeAsync<LlamaCppRuntimeManifest>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
            if (cached != null && !string.Equals(cached.RuntimeVersion, bundled.RuntimeVersion, StringComparison.OrdinalIgnoreCase))
                return cached;
        }
        catch { /* Cache file is corrupt; fall through to bundled */ }
        return bundled;
    }

    /// <summary>Deletes the local manifest cache file after a successful install.</summary>
    private void DeleteManifestCacheFile()
    {
        var cachePath = Path.Combine(storage.LlamaCppRoot, "LlamaCppRuntimeManifestCache.json");
        try { if (File.Exists(cachePath)) File.Delete(cachePath); } catch { /* best effort */ }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlamaCppRuntimeAsset>> GetAvailableAssetsAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await GetEffectiveManifestAsync(cancellationToken);
        return manifest.Assets.Where(x => !string.IsNullOrWhiteSpace(x.EntryExe)).ToList();
    }

    public async Task<LlamaCppRuntimeAsset> GetSelectedAssetAsync(CancellationToken cancellationToken = default)
    {
        var defaultRuntimeId = GetDefaultRuntimeId(RuntimeInformation.ProcessArchitecture);
        var assets = await GetAvailableAssetsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.Settings.SelectedLlamaRuntimeId))
            settings.Settings.SelectedLlamaRuntimeId = defaultRuntimeId;
        return assets.SingleOrDefault(x => x.Id == settings.Settings.SelectedLlamaRuntimeId)
               ?? throw new InvalidDataException($"Runtime '{settings.Settings.SelectedLlamaRuntimeId}' is unavailable for selection.");
    }

    public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        var asset = await GetSelectedAssetAsync(cancellationToken);
        return File.Exists(GetExecutablePath(asset));
    }

    /// <inheritdoc />
    public Task<bool> IsAssetInstalledAsync(string assetId, string entryExe, CancellationToken cancellationToken = default)
    {
        var exePath = Path.Combine(storage.LlamaCppRoot, "Runtimes", assetId, "current", entryExe);
        return Task.FromResult(File.Exists(exePath));
    }

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (_downloadCts != null) throw new InvalidOperationException("A llama.cpp runtime download is already active.");
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string? staging = null;
        try
        {
            var manifest = await GetEffectiveManifestAsync(_downloadCts.Token);
            var selected = await GetSelectedAssetAsync(_downloadCts.Token);
            var installAssets = new List<LlamaCppRuntimeAsset> { selected };
            installAssets.AddRange(selected.RequiredExtraAssets.Select(id => manifest.Assets.Single(x => x.Id == id)));
            var runtimeRoot = GetRuntimeRoot(selected);
            await Task.Run(() => Directory.CreateDirectory(runtimeRoot), _downloadCts.Token).ConfigureAwait(false);
            var currentManifest = await ReadInstallManifestAsync(Path.Combine(runtimeRoot, "current", "manifest.json"), _downloadCts.Token);
            if (currentManifest is not null &&
                string.Equals(currentManifest.AssetId, selected.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(currentManifest.RuntimeVersion, manifest.RuntimeVersion, StringComparison.OrdinalIgnoreCase) &&
                await IsInstalledAsync(_downloadCts.Token))
            {
                Set(new(false, 100, "SmartBpAiStatusInstalled"));
                return;
            }
            staging = Path.Combine(runtimeRoot, $"staging-{DateTime.Now:yyyyMMdd-HHmmssfff}");
            await Task.Run(() => Directory.CreateDirectory(staging), _downloadCts.Token).ConfigureAwait(false);
            Set(new(true, 0, "SmartBpAiRuntimeDownloading"));
            for (var index = 0; index < installAssets.Count; index++)
            {
                var asset = installAssets[index];
                var archive = Path.Combine(runtimeRoot, $".{asset.Id}-{Guid.NewGuid():N}.zip");
                try
                {
                    await DownloadAsync(asset, archive, index, installAssets.Count, _downloadCts.Token);
                    await Task.Run(() => ZipFile.ExtractToDirectory(archive, staging, true), _downloadCts.Token).ConfigureAwait(false);
                }
                finally { if (File.Exists(archive)) File.Delete(archive); }
            }
            var stagedExe = Directory.EnumerateFiles(staging, selected.EntryExe!, SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new FileNotFoundException($"{selected.EntryExe} was not found in the downloaded runtime.");
            await SmokeCheckAsync(stagedExe, _downloadCts.Token);
            await File.WriteAllTextAsync(Path.Combine(staging, "manifest.json"),
                JsonSerializer.Serialize(new RuntimeInstallManifest(selected.Id, manifest.RuntimeVersion), new JsonSerializerOptions { WriteIndented = true }),
                _downloadCts.Token).ConfigureAwait(false);
            var current = Path.Combine(runtimeRoot, "current");
            var previous = Path.Combine(runtimeRoot, "previous");
            await Task.Run(() => CommitStaging(staging, current, previous), _downloadCts.Token).ConfigureAwait(false);
            staging = null;
            settings.Settings.LlamaServerExecutablePath = Directory.EnumerateFiles(current, selected.EntryExe!, SearchOption.AllDirectories).First();
            await settings.SaveAsync(_downloadCts.Token);
            Set(new(false, 100, "SmartBpAiStatusInstalled"));
            DeleteManifestCacheFile();
            // Clean up old version after successful update; rollback is no longer possible
            await Task.Run(() => { if (Directory.Exists(previous)) Directory.Delete(previous, true); }).ConfigureAwait(false);
            debugLog.Write("runtime", $"Installed {selected.DisplayName}: {settings.Settings.LlamaServerExecutablePath}");
        }
        catch (OperationCanceledException) { Set(new(false, null, "SmartBpAiStatusCancelled")); throw; }
        catch (Exception ex) { Set(new(false, null, "SmartBpDownloadFailedSimple", ErrorMessage: ex.ToString())); logger.LogError(ex, "llama.cpp runtime installation failed"); throw; }
        finally
        {
            if (staging != null)
            {
                var cleanup = staging;
                await Task.Run(() => { if (Directory.Exists(cleanup)) Directory.Delete(cleanup, true); }).ConfigureAwait(false);
            }
            _downloadCts?.Dispose(); _downloadCts = null;
        }
    }

    public void Cancel() => _downloadCts?.Cancel();

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (_downloadCts != null) throw new InvalidOperationException("Cannot delete a runtime while downloading.");
        var asset = await GetSelectedAssetAsync(cancellationToken).ConfigureAwait(false);
        var root = GetRuntimeRoot(asset);
        await Task.Run(() => { if (Directory.Exists(root)) Directory.Delete(root, true); }, cancellationToken).ConfigureAwait(false);
        if (settings.Settings.LlamaServerExecutablePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            settings.Settings.LlamaServerExecutablePath = "";
            await settings.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        Set(new(false, null, "SmartBpAiStatusNotInstalled"));
    }

    public async Task<string> GetInstalledExecutablePathAsync(CancellationToken cancellationToken = default)
    {
        var asset = await GetSelectedAssetAsync(cancellationToken);
        var path = GetExecutablePath(asset);
        if (File.Exists(path)) return path;
        var current = Path.Combine(GetRuntimeRoot(asset), "current");
        return Directory.Exists(current)
            ? Directory.EnumerateFiles(current, asset.EntryExe!, SearchOption.AllDirectories).FirstOrDefault() ?? throw new FileNotFoundException("The selected llama.cpp runtime is not installed.")
            : throw new FileNotFoundException("The selected llama.cpp runtime is not installed.");
    }

    public async Task<bool> CanRollbackAsync(CancellationToken cancellationToken = default)
    {
        var asset = await GetSelectedAssetAsync(cancellationToken).ConfigureAwait(false);
        var previous = Path.Combine(GetRuntimeRoot(asset), "previous");
        return Directory.Exists(previous) && File.Exists(Path.Combine(previous, "manifest.json")) &&
               Directory.EnumerateFiles(previous, asset.EntryExe!, SearchOption.AllDirectories).Any();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_downloadCts is not null) throw new InvalidOperationException("Cannot roll back while downloading.");
        var asset = await GetSelectedAssetAsync(cancellationToken).ConfigureAwait(false);
        var root = GetRuntimeRoot(asset);
        var current = Path.Combine(root, "current");
        var previous = Path.Combine(root, "previous");
        if (!await CanRollbackAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("No previous llama.cpp runtime is available.");
        var swap = Path.Combine(root, $"swap-{Guid.NewGuid():N}");
        await Task.Run(() =>
        {
            Directory.Move(current, swap);
            try
            {
                Directory.Move(previous, current);
                Directory.Move(swap, previous);
            }
            catch
            {
                if (!Directory.Exists(current) && Directory.Exists(swap)) Directory.Move(swap, current);
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
        settings.Settings.LlamaServerExecutablePath = Directory.EnumerateFiles(current, asset.EntryExe!, SearchOption.AllDirectories).First();
        await settings.SaveAsync(cancellationToken).ConfigureAwait(false);
        Set(new(false, 100, "SmartBpAiRuntimeRollbackComplete"));
    }

    private string GetRuntimeRoot(LlamaCppRuntimeAsset asset) => Path.Combine(storage.LlamaCppRoot, "Runtimes", asset.Id);
    private string GetExecutablePath(LlamaCppRuntimeAsset asset) => Path.Combine(GetRuntimeRoot(asset), "current", asset.EntryExe!);

    private async Task DownloadAsync(LlamaCppRuntimeAsset asset, string path, int index, int count, CancellationToken token)
    {
        var url = asset.UrlIsDirectDownload ? asset.Url : await urlResolver.ResolveAsync(asset.Url, token);
        debugLog.Write("runtime", $"Downloading {asset.Id} from {url}");
        var fileName = Path.GetFileName(path);
        await SmartBpParallelDownload.DownloadFileAsync(
            url,
            path,
            token,
            progress =>
            {
                var length = progress.TotalBytesToReceive > 0 ? progress.TotalBytesToReceive : (long?)null;
                var overallProgress = count > 0
                    ? (index + progress.ProgressPercentage / 100D) / count * 100D
                    : 100D;
                TimeSpan? eta = length is > 0 && progress.BytesPerSecondSpeed > 1
                    ? TimeSpan.FromSeconds(Math.Max(0, length.Value - progress.ReceivedBytesSize) / progress.BytesPerSecondSpeed)
                    : null;
                Set(new(
                    true,
                    overallProgress,
                    "SmartBpAiRuntimeDownloading",
                    fileName,
                    progress.ReceivedBytesSize,
                    length,
                    progress.BytesPerSecondSpeed,
                    eta));
            }).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(asset.Sha256))
        {
            await using var verify = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(verify, token));
            if (!actual.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"SHA256 validation failed for {asset.Id}.");
        }
    }

    private static async Task SmokeCheckAsync(string executable, CancellationToken token)
    {
        using var process = await Task.Run(() => Process.Start(new ProcessStartInfo(executable, "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }), token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Failed to start the downloaded llama-server executable.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { process.Kill(true); throw new InvalidDataException("llama-server --version smoke check timed out."); }
        if (process.ExitCode != 0) throw new InvalidDataException($"llama-server smoke check failed with exit code {process.ExitCode}.");
    }

    private void Set(LlamaCppRuntimeInstallState state) { State = state; StateChanged?.Invoke(this, state); }

    private static void CommitStaging(string staging, string current, string previous)
    {
        if (Directory.Exists(previous)) Directory.Delete(previous, true);
        if (Directory.Exists(current)) Directory.Move(current, previous);
        try { Directory.Move(staging, current); }
        catch
        {
            if (!Directory.Exists(current) && Directory.Exists(previous)) Directory.Move(previous, current);
            throw;
        }
    }

    private static async Task<RuntimeInstallManifest?> ReadInstallManifestAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RuntimeInstallManifest>(stream, cancellationToken: token).ConfigureAwait(false);
    }

    private sealed record RuntimeInstallManifest(string AssetId, string RuntimeVersion);

    internal static string GetDefaultRuntimeId(Architecture architecture) => architecture switch
    {
        Architecture.X64 => "win-x64-cpu",
        Architecture.Arm64 => "win-arm64-cpu",
        Architecture.X86 => throw new PlatformNotSupportedException("32-bit Windows is not supported by the managed llama.cpp runtime."),
        _ => throw new PlatformNotSupportedException($"Architecture {architecture} is not supported by the managed llama.cpp runtime.")
    };
}
