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
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class LlamaCppRuntimeManifestProvider : ILlamaCppRuntimeManifestProvider
{
    public async Task<LlamaCppRuntimeManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppConstants.ResourcesPath, "SmartBp", "LlamaCppRuntimeManifest.json");
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<LlamaCppRuntimeManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
            ?? throw new InvalidDataException("llama.cpp runtime manifest is empty.");
        if (manifest.SchemaVersion != 1 || manifest.Assets.Count == 0) throw new InvalidDataException("Unsupported llama.cpp runtime manifest.");
        if (!manifest.ReleasePage.EndsWith('/' + manifest.RuntimeVersion, StringComparison.OrdinalIgnoreCase) ||
            manifest.Assets.Any(x => !x.Url.Contains('/' + manifest.RuntimeVersion + '/', StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("llama.cpp runtime manifest version is inconsistent with its release URLs.");
        return manifest;
    }
}

internal sealed class LlamaCppRuntimeAssetManager(
    ILlamaCppRuntimeManifestProvider manifestProvider,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpModuleStorageProvider storage,
    IGitHubDownloadUrlResolver urlResolver,
    ISmartBpDebugLog debugLog,
    ILogger<LlamaCppRuntimeAssetManager> logger) : ILlamaCppRuntimeAssetManager
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36";
    private CancellationTokenSource? _downloadCts;
    public event EventHandler<LlamaCppRuntimeInstallState>? StateChanged;
    public LlamaCppRuntimeInstallState State { get; private set; } = new(false, null, "SmartBpAiStatusNotInstalled");

    public async Task<IReadOnlyList<LlamaCppRuntimeAsset>> GetAvailableAssetsAsync(CancellationToken cancellationToken = default)
        => (await manifestProvider.LoadAsync(cancellationToken)).Assets.Where(x => !string.IsNullOrWhiteSpace(x.EntryExe)).ToList();

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

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (_downloadCts != null) throw new InvalidOperationException("A llama.cpp runtime download is already active.");
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string? staging = null;
        try
        {
            var manifest = await manifestProvider.LoadAsync(_downloadCts.Token);
            var selected = await GetSelectedAssetAsync(_downloadCts.Token);
            var installAssets = new List<LlamaCppRuntimeAsset> { selected };
            installAssets.AddRange(selected.RequiredExtraAssets.Select(id => manifest.Assets.Single(x => x.Id == id)));
            var runtimeRoot = GetRuntimeRoot(selected);
            await Task.Run(() => Directory.CreateDirectory(runtimeRoot), _downloadCts.Token).ConfigureAwait(false);
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
            var current = Path.Combine(runtimeRoot, "current");
            var previous = Path.Combine(runtimeRoot, "previous");
            await Task.Run(() => CommitStaging(staging, current, previous), _downloadCts.Token).ConfigureAwait(false);
            staging = null;
            settings.Settings.LlamaServerExecutablePath = Directory.EnumerateFiles(current, selected.EntryExe!, SearchOption.AllDirectories).First();
            await settings.SaveAsync(_downloadCts.Token);
            Set(new(false, 100, "SmartBpAiStatusInstalled"));
            debugLog.Write("runtime", $"Installed {selected.DisplayName}: {settings.Settings.LlamaServerExecutablePath}");
        }
        catch (OperationCanceledException) { Set(new(false, null, "SmartBpAiStatusCancelled")); throw; }
        catch (Exception ex) { Set(new(false, null, ex.Message)); logger.LogError(ex, "llama.cpp runtime installation failed"); throw; }
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

    private string GetRuntimeRoot(LlamaCppRuntimeAsset asset) => Path.Combine(storage.LlamaCppRoot, "Runtimes", asset.Id);
    private string GetExecutablePath(LlamaCppRuntimeAsset asset) => Path.Combine(GetRuntimeRoot(asset), "current", asset.EntryExe!);

    private async Task DownloadAsync(LlamaCppRuntimeAsset asset, string path, int index, int count, CancellationToken token)
    {
        var url = await urlResolver.ResolveAsync(asset.Url, token);
        debugLog.Write("runtime", $"Downloading {asset.Id} from {url}");
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
        var buffer = new byte[131072]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token); total += read;
            if (length > 0) Set(new(true, (index + (double)total / length.Value) / count * 100, "SmartBpAiRuntimeDownloading"));
        }
        await output.FlushAsync(token);
        if (!string.IsNullOrWhiteSpace(asset.Sha256))
        {
            await output.DisposeAsync();
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

    internal static string GetDefaultRuntimeId(Architecture architecture) => architecture switch
    {
        Architecture.X64 => "win-x64-cpu",
        Architecture.Arm64 => "win-arm64-cpu",
        Architecture.X86 => throw new PlatformNotSupportedException("32-bit Windows is not supported by the managed llama.cpp runtime."),
        _ => throw new PlatformNotSupportedException($"Architecture {architecture} is not supported by the managed llama.cpp runtime.")
    };
}
