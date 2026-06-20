using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>Downloads and validates managed Tesseract language data.</summary>
public sealed class TesseractDataAssetManager(
    ISmartBpModuleStorageProvider storage,
    ISmartBpRecognitionSettingsService settingsService,
    IGitHubDownloadUrlResolver urlResolver) : ITesseractDataAssetManager
{
    private static readonly TesseractManagedLanguage[] Assets =
    [
        new("chi_sim", "SmartBpTesseractLanguageChineseSimplified", "https://github.com/tesseract-ocr/tessdata/raw/main/chi_sim.traineddata"),
        new("eng", "SmartBpTesseractLanguageEnglish", "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata"),
        new("jpn", "SmartBpTesseractLanguageJapanese", "https://github.com/tesseract-ocr/tessdata/raw/main/jpn.traineddata")
    ];
    private static readonly TimeSpan ResponseHeaderTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadStallTimeout = TimeSpan.FromSeconds(30);
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
    private CancellationTokenSource? _downloadCts;

    /// <inheritdoc />
    public event EventHandler<SmartBpDownloadState>? StateChanged;

    /// <inheritdoc />
    public Task<TesseractDataStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetManagedPath();
        var installed = Assets.Select(x => x.Language)
            .Where(language => File.Exists(Path.Combine(path, $"{language}.traineddata")))
            .ToArray();
        var required = ParseLanguages(settingsService.Settings.TesseractLanguages).ToArray();
        var missing = required.Except(installed, StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.FromResult(new TesseractDataStatus(missing.Length == 0, path, missing, installed));
    }

    /// <inheritdoc />
    public IReadOnlyList<TesseractLanguageAsset> GetAvailableLanguages() =>
        Assets.Select(asset => new TesseractLanguageAsset(asset.Language, asset.DisplayNameKey)).ToArray();

    /// <inheritdoc />
    public async Task InstallLanguagesAsync(IEnumerable<string> languages, CancellationToken cancellationToken = default)
    {
        if (_downloadCts is not null) throw new InvalidOperationException("A Tesseract language-data download is already active.");
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            urlResolver.ResetCache();
            var path = GetManagedPath();
            Directory.CreateDirectory(path);
            var selected = ResolveAssets(languages).ToArray();
            for (var index = 0; index < selected.Length; index++)
                await DownloadAsync(selected[index], path, index, selected.Length, _downloadCts.Token).ConfigureAwait(false);
            Raise(new(false, 100, "SmartBpTesseractDataInstalled"));
        }
        catch (OperationCanceledException)
        {
            Raise(new(false, null, "SmartBpDownloadCancelled"));
            throw;
        }
        catch (Exception ex)
        {
            Raise(new(false, null, "SmartBpDownloadFailedSimple", ErrorMessage: ex.ToString()));
            throw;
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(IEnumerable<string> languages, CancellationToken cancellationToken = default)
    {
        if (_downloadCts is not null) throw new InvalidOperationException("Cannot delete Tesseract data while downloading.");
        var path = GetManagedPath();
        var selected = ResolveAssets(languages).ToArray();
        await Task.Run(() =>
        {
            foreach (var asset in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = Path.Combine(path, $"{asset.Language}.traineddata");
                if (File.Exists(file)) File.Delete(file);
            }
        }, cancellationToken).ConfigureAwait(false);
        Raise(new(false, null, "SmartBpTesseractDataMissing"));
    }

    /// <inheritdoc />
    public void Cancel() => _downloadCts?.Cancel();

    private async Task DownloadAsync(TesseractManagedLanguage asset, string directory, int index, int count, CancellationToken token)
    {
        var destination = Path.Combine(directory, $"{asset.Language}.traineddata");
        var fileName = Path.GetFileName(destination);
        var baseProgress = count == 0 ? 100 : index / (double)count * 100;
        if (File.Exists(destination))
        {
            Raise(new(true, count == 0 ? 100 : (index + 1D) / count * 100, "SmartBpTesseractDataDownloading", fileName));
            return;
        }

        var temporary = destination + ".download";
        try
        {
            Raise(new(true, baseProgress, "SmartBpTesseractDataDownloading", fileName));
            var resolvedUrl = await urlResolver.ResolveAsync(asset.Url, token).ConfigureAwait(false);
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            ConfigureDownloadHeaders(client, new Uri(resolvedUrl));
            using var response = await GetResponseAsync(client, resolvedUrl, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength;
            var watch = Stopwatch.StartNew();
            long received = 0;
            {
                await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                await using var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true);
                var buffer = new byte[131072];
                int read;
                while ((read = await ReadWithStallTimeoutAsync(input, buffer, token).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    received += read;
                    var speed = received / Math.Max(.001, watch.Elapsed.TotalSeconds);
                    TimeSpan? eta = length is > 0 && speed > 1 ? TimeSpan.FromSeconds((length.Value - received) / speed) : null;
                    double? progress = length is > 0 && count > 0
                        ? (index + Math.Min(.98, received / (double)length.Value * .98)) / count * 100
                        : null;
                    Raise(new(true, progress, "SmartBpTesseractDataDownloading", Path.GetFileName(destination), received, length, speed, eta));
                }
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            if (length is > 0 && received != length.Value)
            {
                throw new InvalidDataException(
                    $"Tesseract language-data download ended early for {fileName}: received {received} of {length.Value} bytes.");
            }
            File.Move(temporary, destination, true);
            Raise(new(true, count == 0 ? 100 : (index + 1D) / count * 100, "SmartBpTesseractDataDownloading", fileName));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string GetManagedPath() => storage.TesseractDataRoot;

    private void Raise(SmartBpDownloadState state) => StateChanged?.Invoke(this, state);

    private static void ConfigureDownloadHeaders(HttpClient client, Uri downloadUri)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
        client.DefaultRequestHeaders.Referrer = new Uri(downloadUri.GetLeftPart(UriPartial.Authority) + "/");
    }

    private static async Task<HttpResponseMessage> GetResponseAsync(HttpClient client, string url, CancellationToken token)
    {
        try
        {
            return await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
                .WaitAsync(ResponseHeaderTimeout, token)
                .ConfigureAwait(false);
        }
        catch (TimeoutException ex) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("Tesseract language-data download timed out while waiting for response headers.", ex);
        }
    }

    private static async Task<int> ReadWithStallTimeoutAsync(Stream input, byte[] buffer, CancellationToken token)
    {
        try
        {
            return await input.ReadAsync(buffer, token)
                .AsTask()
                .WaitAsync(ReadStallTimeout, token)
                .ConfigureAwait(false);
        }
        catch (TimeoutException ex) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("Tesseract language-data download stalled because no data was received for 30 seconds.", ex);
        }
    }

    private static IEnumerable<TesseractManagedLanguage> ResolveAssets(IEnumerable<string> languages)
    {
        var requested = languages
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var language in requested)
        {
            var asset = Assets.FirstOrDefault(item => item.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
            if (asset is null)
                throw new InvalidOperationException($"Unsupported Tesseract language data asset: {language}");
            yield return asset;
        }
    }

    private static IEnumerable<string> ParseLanguages(string? languages) =>
        (string.IsNullOrWhiteSpace(languages) ? "chi_sim+eng" : languages)
        .Split(['+', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    private sealed record TesseractManagedLanguage(string Language, string DisplayNameKey, string Url);
}
