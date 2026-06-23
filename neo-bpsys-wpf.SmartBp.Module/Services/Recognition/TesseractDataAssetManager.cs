using System.IO;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 下载并校验 SmartBP 托管的 Tesseract 语言数据。
/// </summary>
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

    /// <summary>
    /// 下载单个 Tesseract 语言数据文件并汇总整体进度。
    /// </summary>
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

        Raise(new(true, baseProgress, "SmartBpTesseractDataDownloading", fileName));
        var resolvedUrl = await urlResolver.ResolveAsync(asset.Url, token).ConfigureAwait(false);
        await SmartBpParallelDownload.DownloadFileAsync(
            resolvedUrl,
            destination,
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
                Raise(new(
                    true,
                    overallProgress,
                    "SmartBpTesseractDataDownloading",
                    fileName,
                    progress.ReceivedBytesSize,
                    length,
                    progress.BytesPerSecondSpeed,
                    eta));
            }).ConfigureAwait(false);
        Raise(new(true, count == 0 ? 100 : (index + 1D) / count * 100, "SmartBpTesseractDataDownloading", fileName));
    }

    /// <summary>
    /// 获取模块托管的 tessdata 目录。
    /// </summary>
    private string GetManagedPath() => storage.TesseractDataRoot;

    /// <summary>
    /// 发布下载状态变化事件。
    /// </summary>
    private void Raise(SmartBpDownloadState state) => StateChanged?.Invoke(this, state);

    /// <summary>
    /// 将语言代码解析为受支持的托管语言资产。
    /// </summary>
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

    /// <summary>
    /// 解析设置中的 Tesseract 语言表达式。
    /// </summary>
    private static IEnumerable<string> ParseLanguages(string? languages) =>
        (string.IsNullOrWhiteSpace(languages) ? "chi_sim+eng" : languages)
        .Split(['+', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    private sealed record TesseractManagedLanguage(string Language, string DisplayNameKey, string Url);
}
