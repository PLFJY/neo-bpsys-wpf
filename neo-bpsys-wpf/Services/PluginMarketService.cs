using Downloader;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Services;

public class PluginMarketService : IPluginMarketService
{
    private const string MarketIndexUrl =
        "https://bpsys-plugin-index.plfjy.top/";

    private readonly HttpClient _httpClient;
    private readonly DownloadService _downloader;
    private readonly ILogger<PluginMarketService> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly Lock _downloadLock = new();
    private CancellationTokenSource? _downloadCts;
    private readonly Dictionary<string, string> _resolvedMirrorCache = new(StringComparer.Ordinal);

    public PluginMarketService(ILogger<PluginMarketService> logger, ISettingsHostService settingsHostService)
    {
        _logger = logger;
        _settingsHostService = settingsHostService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", AppConstants.AppName);

        var downloadOpt = new DownloadConfiguration
        {
            ChunkCount = 8,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 5,
            ParallelCount = 6,
        };
        _downloader = new DownloadService(downloadOpt);
        _downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
    }

    public bool IsDownloading { get; private set; }

    public double DownloadProgress { get; private set; }

    public double DownloadBytesPerSecond { get; private set; }

    public string CurrentDownloadPluginId { get; private set; } = string.Empty;

    public event EventHandler? DownloadStateChanged;

    public async Task<IReadOnlyList<PluginMarketItem>> GetMarketPluginsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(await ResolveGitHubUrlAsync(MarketIndexUrl, cancellationToken),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, PluginMarketItem>>(content, options) ?? [];
        var items = new List<PluginMarketItem>();
        foreach (var (key, value) in dictionary.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            value.Id = string.IsNullOrWhiteSpace(value.Id) ? key : value.Id;
            value.ResolvedIconUrl = await ResolveGitHubUrlAsync(value.Icon, cancellationToken);
            value.ResolvedReadmeUrl = await ResolveGitHubUrlAsync(value.Readme, cancellationToken);
            value.ResolvedDownloadUrl = await ResolveGitHubUrlAsync(value.DownloadUrl, cancellationToken);
            items.Add(value);
        }

        return items;
    }

    public async Task<string> GetReadmeMarkdownAsync(PluginMarketItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.ResolvedReadmeUrl))
        {
            return string.Empty;
        }

        var response = await _httpClient.GetAsync(item.ResolvedReadmeUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var markdown = await response.Content.ReadAsStringAsync(cancellationToken);
        var baseReadmeUrl = Uri.TryCreate(item.Readme, UriKind.Absolute, out _)
            ? item.Readme
            : item.ResolvedReadmeUrl;
        return RewriteRelativeMarkdownLinks(markdown, baseReadmeUrl);
    }

    public async Task<PluginPackageDownloadResult> DownloadPluginPackageAsync(PluginMarketItem item,
        CancellationToken cancellationToken = default)
    {
        var tempZipPath = Path.Combine(AppConstants.AppTempPath, $"plugin-market-{item.Id}.zip");
        var extractPath = Path.Combine(AppConstants.AppTempPath, "PluginMarket", item.Id);

        if (File.Exists(tempZipPath))
        {
            File.Delete(tempZipPath);
        }

        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }

        CancellationTokenSource localCts;
        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                throw new InvalidOperationException(I18nHelper.GetLocalizedString("PluginMarketDownloadAlreadyInProgress"));
            }

            IsDownloading = true;
            DownloadProgress = 0;
            DownloadBytesPerSecond = 0;
            CurrentDownloadPluginId = item.Id;
            _downloadCts = new CancellationTokenSource();
            localCts = _downloadCts;
        }

        RaiseDownloadStateChanged();

        try
        {
            await _downloader.DownloadFileTaskAsync(item.ResolvedDownloadUrl, tempZipPath);
            cancellationToken.ThrowIfCancellationRequested();

            if (localCts.IsCancellationRequested)
            {
                throw new OperationCanceledException(localCts.Token);
            }

            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(tempZipPath, extractPath, true);

            return new PluginPackageDownloadResult
            {
                ExtractedDirectoryPath = extractPath
            };
        }
        catch (OperationCanceledException)
        {
            CleanupDownloadArtifacts(tempZipPath, extractPath);
            throw;
        }
        catch (Exception ex)
        {
            CleanupDownloadArtifacts(tempZipPath, extractPath);
            _logger.LogError(ex, "Error downloading plugin package for {PluginId}", item.Id);
            throw;
        }
        finally
        {
            lock (_downloadLock)
            {
                IsDownloading = false;
                DownloadProgress = 0;
                DownloadBytesPerSecond = 0;
                CurrentDownloadPluginId = string.Empty;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }

            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }

            RaiseDownloadStateChanged();
        }
    }

    public void CancelDownload()
    {
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
        }

        _downloader.CancelAsync();
    }

    public void ResetMirrorCache()
    {
        lock (_resolvedMirrorCache)
        {
            _resolvedMirrorCache.Clear();
        }
    }

    private async Task<string> ResolveGitHubUrlAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!ShouldApplyGhProxy(url))
        {
            return url;
        }

        var preferredMirror = _settingsHostService.Settings.PluginMarketMirror;
        if (string.IsNullOrWhiteSpace(preferredMirror))
        {
            return url;
        }

        string? resolvedMirror;
        lock (_resolvedMirrorCache)
        {
            _resolvedMirrorCache.TryGetValue(preferredMirror, out resolvedMirror);
        }

        if (!string.IsNullOrWhiteSpace(resolvedMirror))
        {
            return resolvedMirror + url;
        }

        var candidates = new List<string> { preferredMirror };
        candidates.AddRange(DownloadMirrorPresets.GhProxyMirrorList.Where(x =>
            !string.IsNullOrWhiteSpace(x) && !string.Equals(x, preferredMirror, StringComparison.OrdinalIgnoreCase)));

        foreach (var mirror in candidates)
        {
            if (await IsMirrorAvailableAsync(mirror, cancellationToken))
            {
                lock (_resolvedMirrorCache)
                {
                    _resolvedMirrorCache[preferredMirror] = mirror;
                }

                return mirror + url;
            }
        }

        lock (_resolvedMirrorCache)
        {
            _resolvedMirrorCache[preferredMirror] = string.Empty;
        }

        return url;
    }

    private bool ShouldApplyGhProxy(string url)
    {
        if (!IsChineseEnvironment())
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
               || uri.Host.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsChineseEnvironment()
    {
        return _settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsMirrorAvailableAsync(string mirror, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, mirror + MarketIndexUrl);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(4));
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                linkedCts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Mirror unavailable: {Mirror}", mirror);
            return false;
        }
    }

    private void Downloader_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        lock (_downloadLock)
        {
            DownloadProgress = e.ProgressPercentage;
            DownloadBytesPerSecond = e.BytesPerSecondSpeed;
        }

        RaiseDownloadStateChanged();
    }

    private void RaiseDownloadStateChanged()
    {
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void CleanupDownloadArtifacts(string tempZipPath, string extractPath)
    {
        if (File.Exists(tempZipPath))
        {
            File.Delete(tempZipPath);
        }

        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
    }

    private static string RewriteRelativeMarkdownLinks(string markdown, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(markdown)
            || string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return markdown;
        }

        markdown = Regex.Replace(
            markdown,
            @"(?<prefix>!\[[^\]]*\]\()(?<target>[^)\s]+)(?<suffix>[^)]*\))",
            match => RewriteMarkdownTarget(match, "target", baseUri),
            RegexOptions.CultureInvariant);

        markdown = Regex.Replace(
            markdown,
            @"(?<prefix>(?<!!)\[[^\]]+\]\()(?<target>[^)\s]+)(?<suffix>[^)]*\))",
            match => RewriteMarkdownTarget(match, "target", baseUri),
            RegexOptions.CultureInvariant);

        markdown = Regex.Replace(
            markdown,
            @"(?m)^(?<prefix>\[[^\]]+\]:\s*)(?<target>\S+)(?<suffix>.*)$",
            match => RewriteMarkdownTarget(match, "target", baseUri),
            RegexOptions.CultureInvariant);

        markdown = Regex.Replace(
            markdown,
            "(?<attr>href|src)=(?<quote>[\"'])(?<target>[^\"'#][^\"']*)(?<quote2>[\"'])",
            match => RewriteHtmlAttributeTarget(match, "target", baseUri),
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        return markdown;
    }

    private static string RewriteMarkdownTarget(Match match, string groupName, Uri baseUri)
    {
        var target = match.Groups[groupName].Value;
        var resolved = ResolveRelativeTarget(target, baseUri);
        if (resolved == null)
        {
            return match.Value;
        }

        return match.Value.Replace(target, resolved, StringComparison.Ordinal);
    }

    private static string RewriteHtmlAttributeTarget(Match match, string groupName, Uri baseUri)
    {
        var target = match.Groups[groupName].Value;
        var resolved = ResolveRelativeTarget(target, baseUri);
        if (resolved == null)
        {
            return match.Value;
        }

        return match.Value.Replace(target, resolved, StringComparison.Ordinal);
    }

    private static string? ResolveRelativeTarget(string target, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(target)
            || target.StartsWith('#')
            || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || Uri.TryCreate(target, UriKind.Absolute, out _))
        {
            return null;
        }

        return Uri.TryCreate(baseUri, target, out var resolvedUri)
            ? resolvedUri.AbsoluteUri
            : null;
    }
}
