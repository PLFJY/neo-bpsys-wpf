using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class QwenModelManifestProvider(ILogger<QwenModelManifestProvider> logger) : IQwenModelManifestProvider
{
    public async Task<QwenModelManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppConstants.ResourcesPath, "SmartBp", "QwenModelManifest.json");
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<QwenModelManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
            ?? throw new InvalidDataException("Qwen model manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Models.Count == 0) throw new InvalidDataException("Unsupported Qwen model manifest.");
        logger.LogInformation("Qwen manifest loaded. Profiles={Count}", manifest.Models.Count);
        return manifest;
    }
}

internal sealed class SmartBpRecognitionSettingsService : ISmartBpRecognitionSettingsService
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly string _path = Path.Combine(AppConstants.AppDataPath, "SmartBp", "RecognitionSettings.json");
    public SmartBpRecognitionSettings Settings { get; private set; }

    public SmartBpRecognitionSettingsService()
    {
        try { Settings = File.Exists(_path) ? JsonSerializer.Deserialize<SmartBpRecognitionSettings>(File.ReadAllText(_path), Options) ?? new() : new(); }
        catch { Settings = new(); }
        Settings.LlamaServerPort = Math.Clamp(Settings.LlamaServerPort, 1024, 65535);
        Settings.MaxImageWidth = Math.Clamp(Settings.MaxImageWidth, 320, 4096);
        Settings.RecognitionIntervalMs = Math.Clamp(Settings.RecognitionIntervalMs, 500, 5000);
        Settings.CpuThreads = Math.Clamp(Settings.CpuThreads, 1, 64);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(Settings, Options), cancellationToken);
        File.Move(temporary, _path, true);
    }
}

internal sealed class QwenModelAssetManager(
    IQwenModelManifestProvider manifestProvider,
    ISmartBpRecognitionSettingsService settingsService,
    ISmartBpModuleStorageProvider storage,
    ILogger<QwenModelAssetManager> logger) : IQwenModelAssetManager
{
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private CancellationTokenSource? _downloadCts;
    public event EventHandler<QwenDownloadState>? StateChanged;
    public QwenDownloadState State { get; private set; } = new(false, null, "SmartBpAiStatusNotInstalled");

    public async Task<QwenModelProfile> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await manifestProvider.LoadAsync(cancellationToken);
        return manifest.Models.SingleOrDefault(x => x.Id == settingsService.Settings.SelectedQwenModelId)
            ?? throw new InvalidDataException($"Qwen profile '{settingsService.Settings.SelectedQwenModelId}' is not present in the manifest.");
    }

    public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(cancellationToken); var paths = GetPaths(p);
        var installed = await MatchesAsync(paths.Model, p.Sha256, cancellationToken) && await MatchesAsync(paths.Mmproj, p.MmprojSha256, cancellationToken);
        logger.LogInformation("Qwen model install status. Model={ModelId}, Installed={Installed}", p.Id, installed);
        return installed;
    }

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (_downloadCts != null) throw new InvalidOperationException("A Qwen download is already active.");
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var p = await GetProfileAsync(_downloadCts.Token); var paths = GetPaths(p); Directory.CreateDirectory(paths.Root);
            Set(new(true, 0, "SmartBpAiStatusDownloading")); logger.LogInformation("Qwen model download started. Model={ModelId}", p.Id);
            await DownloadAsync(p.ModelUrl, paths.Model, p.Sha256, 0, 50, _downloadCts.Token);
            await DownloadAsync(p.MmprojUrl, paths.Mmproj, p.MmprojSha256, 50, 100, _downloadCts.Token);
            Set(new(false, 100, "SmartBpAiStatusInstalled")); logger.LogInformation("Qwen model download completed. Model={ModelId}", p.Id);
        }
        catch (OperationCanceledException) { Set(new(false, null, "SmartBpAiStatusCancelled")); logger.LogInformation("Qwen model download cancelled"); throw; }
        catch (Exception ex) { Set(new(false, null, ex.Message)); logger.LogError(ex, "Qwen model download failed"); throw; }
        finally { _downloadCts?.Dispose(); _downloadCts = null; }
    }

    public void Cancel() => _downloadCts?.Cancel();
    public void Delete()
    {
        if (_downloadCts != null) throw new InvalidOperationException("Cannot delete while downloading.");
        var p = GetProfileAsync().GetAwaiter().GetResult(); var root = GetPaths(p).Root;
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Set(new(false, null, "SmartBpAiStatusNotInstalled"));
    }

    public async Task<(string ModelPath, string MmprojPath)> GetInstalledPathsAsync(CancellationToken cancellationToken = default)
    {
        var p = await GetProfileAsync(cancellationToken); var paths = GetPaths(p);
        if (!await IsInstalledAsync(cancellationToken)) throw new FileNotFoundException("The selected Qwen model is not installed.");
        return (paths.Model, paths.Mmproj);
    }

    private (string Root, string Model, string Mmproj) GetPaths(QwenModelProfile p)
    {
        var root = Path.Combine(storage.QwenModelsRoot, p.Id);
        return (root, Path.Combine(root, Path.GetFileName(p.ModelFileName)), Path.Combine(root, Path.GetFileName(p.MmprojFileName)));
    }

    private async Task DownloadAsync(string url, string finalPath, string? hash, double from, double to, CancellationToken token)
    {
        if (await MatchesAsync(finalPath, hash, token)) return;
        var temp = finalPath + ".download"; if (File.Exists(temp)) File.Delete(temp);
        try
        {
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            ConfigureDownloadHeaders(http, new Uri(url));
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength; await using var input = await response.Content.ReadAsStreamAsync(token);
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true))
            {
                var buffer = new byte[1024 * 128]; long readTotal = 0; int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0) { await output.WriteAsync(buffer.AsMemory(0, read), token); readTotal += read; if (total > 0) Set(new(true, from + (to - from) * readTotal / total.Value, State.Status)); }
                await output.FlushAsync(token);
            }
            if (!await MatchesAsync(temp, hash, token)) throw new InvalidDataException($"SHA256 validation failed for {Path.GetFileName(finalPath)}.");
            File.Move(temp, finalPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
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
