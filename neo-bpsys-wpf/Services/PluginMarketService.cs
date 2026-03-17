using Downloader;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 插件市场服务实现。
/// </summary>
public class PluginMarketService : IPluginMarketService
{
    private const string MarketIndexUrl =
        "https://bpsys-plugin-index.plfjy.top/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PluginMarketService> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly Lock _downloadLock = new();
    private CancellationTokenSource? _downloadCts;
    private DownloadService? _currentDownloader;
    private QueuedPluginDownloadRequest? _currentDownloadRequest;
    private readonly ObservableCollection<PluginDownloadQueueItem> _downloadQueueInternal = [];
    private readonly Queue<QueuedPluginDownloadRequest> _pendingDownloads = new();
    private readonly Dictionary<string, string> _resolvedMirrorCache = new(StringComparer.Ordinal);
    private readonly Queue<PluginPackageDownloadResult> _completedDownloadResults = new();
    private bool _isProcessingQueue;

    /// <summary>
    /// 初始化插件市场服务。
    /// </summary>
    public PluginMarketService(ILogger<PluginMarketService> logger, ISettingsHostService settingsHostService)
    {
        _logger = logger;
        _settingsHostService = settingsHostService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", AppConstants.AppName);
        DownloadQueue = new ReadOnlyObservableCollection<PluginDownloadQueueItem>(_downloadQueueInternal);
    }

    /// <summary>
    /// 当前插件下载队列。
    /// </summary>
    public ReadOnlyObservableCollection<PluginDownloadQueueItem> DownloadQueue { get; }

    /// <summary>
    /// 当前是否有任务正在下载。
    /// </summary>
    public bool IsDownloading { get; private set; }

    /// <summary>
    /// 当前是否存在已经下载完成、等待安装的插件包。
    /// </summary>
    public bool IsDownloadFinished { get; private set; }

    /// <summary>
    /// 当前下载进度，范围 0-100。
    /// </summary>
    public double DownloadProgress { get; private set; }

    /// <summary>
    /// 当前下载速度，单位为字节/秒。
    /// </summary>
    public double DownloadBytesPerSecond { get; private set; }

    /// <summary>
    /// 当前正在下载的插件 ID。
    /// </summary>
    public string CurrentDownloadPluginId { get; private set; } = string.Empty;

    /// <summary>
    /// 下载状态发生变化时触发。
    /// </summary>
    public event EventHandler? DownloadStateChanged;

    /// <summary>
    /// 获取插件市场中的插件列表。
    /// </summary>
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

    /// <summary>
    /// 获取插件 README 内容。
    /// </summary>
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

    /// <summary>
    /// 将插件加入下载队列。
    /// </summary>
    public Task<bool> QueuePluginDownloadAsync(PluginMarketItem item,
        CancellationToken cancellationToken = default)
    {
        lock (_downloadLock)
        {
            if (_currentDownloadRequest?.QueueItem.PluginId == item.Id
                || _pendingDownloads.Any(x => x.QueueItem.PluginId == item.Id))
            {
                return Task.FromResult(false);
            }

            var queueItem = new PluginDownloadQueueItem
            {
                PluginId = item.Id,
                PluginName = item.Name,
                PluginVersion = item.Version,
                CanCancel = true
            };
            var request = new QueuedPluginDownloadRequest(item, queueItem, cancellationToken);
            _pendingDownloads.Enqueue(request);
            RunOnUiThread(() => _downloadQueueInternal.Add(queueItem));
        }

        RaiseDownloadStateChanged();
        _ = EnsureQueueProcessorRunningAsync();
        return Task.FromResult(true);
    }

    /// <summary>
    /// 取消当前正在下载的任务。
    /// </summary>
    public void CancelDownload()
    {
        DownloadService? currentDownloader;
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
            currentDownloader = _currentDownloader;
        }

        currentDownloader?.CancelAsync();
    }

    /// <summary>
    /// 取消指定下载任务。
    /// </summary>
    public void CancelDownload(string queueId)
    {
        DownloadService? currentDownloader = null;
        PluginDownloadQueueItem? canceledQueueItem = null;

        lock (_downloadLock)
        {
            if (_currentDownloadRequest?.QueueItem.QueueId == queueId)
            {
                _downloadCts?.Cancel();
                currentDownloader = _currentDownloader;
                canceledQueueItem = _currentDownloadRequest.QueueItem;
            }
            else if (_pendingDownloads.Count > 0)
            {
                var retained = new Queue<QueuedPluginDownloadRequest>();
                while (_pendingDownloads.Count > 0)
                {
                    var request = _pendingDownloads.Dequeue();
                    if (request.QueueItem.QueueId == queueId)
                    {
                        canceledQueueItem = request.QueueItem;
                        continue;
                    }

                    retained.Enqueue(request);
                }

                while (retained.Count > 0)
                {
                    _pendingDownloads.Enqueue(retained.Dequeue());
                }
            }
        }

        if (canceledQueueItem != null && currentDownloader == null)
        {
            UpdateQueueItem(canceledQueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueCanceled;
                queueItem.CanCancel = false;
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = string.Empty;
            });
        }

        currentDownloader?.CancelAsync();
        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 取出一个已下载完成、等待安装的插件包。
    /// </summary>
    public PluginPackageDownloadResult? ConsumeCompletedDownload()
    {
        lock (_downloadLock)
        {
            if (_completedDownloadResults.Count == 0)
            {
                return null;
            }

            var result = _completedDownloadResults.Dequeue();
            IsDownloadFinished = _completedDownloadResults.Count > 0;
            return result;
        }
    }

    /// <summary>
    /// 清空镜像缓存。
    /// </summary>
    public void ResetMirrorCache()
    {
        lock (_resolvedMirrorCache)
        {
            _resolvedMirrorCache.Clear();
        }
    }

    /// <summary>
    /// 解析插件市场和插件包实际使用的下载地址。
    /// </summary>
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

        var preferredMirror = _settingsHostService.Settings.GhProxyMirror;
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

    /// <summary>
    /// 判断指定地址是否需要使用镜像。
    /// </summary>
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

    /// <summary>
    /// 判断当前是否处于中文环境。
    /// </summary>
    private bool IsChineseEnvironment()
    {
        return _settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 检查指定镜像是否可用。
    /// </summary>
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

    /// <summary>
    /// 创建单个插件下载任务使用的下载器。
    /// </summary>
    private DownloadService CreateDownloadService()
    {
        var downloadOpt = new DownloadConfiguration
        {
            ChunkCount = 8,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 5,
            ParallelCount = 6,
        };
        var service = new DownloadService(downloadOpt);
        service.DownloadProgressChanged += Downloader_DownloadProgressChanged;
        return service;
    }

    /// <summary>
    /// 更新当前下载进度和速度。
    /// </summary>
    private void Downloader_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        lock (_downloadLock)
        {
            DownloadProgress = e.ProgressPercentage;
            DownloadBytesPerSecond = e.BytesPerSecondSpeed;
        }

        var currentRequest = _currentDownloadRequest;
        if (currentRequest != null)
        {
            UpdateQueueItem(currentRequest.QueueItem, queueItem =>
            {
                queueItem.Progress = e.ProgressPercentage;
                queueItem.ProgressText = $"{e.ProgressPercentage:0.00}%";
                queueItem.SpeedText = $"{(e.BytesPerSecondSpeed / 1024 / 1024):0.00} MB/s";
            });
        }

        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 通知下载状态已变化。
    /// </summary>
    private void RaiseDownloadStateChanged()
    {
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清理下载过程中产生的临时文件。
    /// </summary>
    private static void CleanupDownloadArtifacts(string downloadSessionPath)
    {
        if (Directory.Exists(downloadSessionPath))
        {
            Directory.Delete(downloadSessionPath, true);
        }
    }

    /// <summary>
    /// 启动下载队列处理。
    /// </summary>
    private async Task EnsureQueueProcessorRunningAsync()
    {
        lock (_downloadLock)
        {
            if (_isProcessingQueue)
            {
                return;
            }

            _isProcessingQueue = true;
        }

        try
        {
            while (true)
            {
                QueuedPluginDownloadRequest? request;
                lock (_downloadLock)
                {
                    if (_pendingDownloads.Count == 0)
                    {
                        _isProcessingQueue = false;
                        return;
                    }

                    request = _pendingDownloads.Dequeue();
                    _currentDownloadRequest = request;
                }

                await ProcessQueuedDownloadAsync(request);
            }
        }
        finally
        {
            lock (_downloadLock)
            {
                _currentDownloadRequest = null;
                _isProcessingQueue = false;
            }

            RaiseDownloadStateChanged();
        }
    }

    /// <summary>
    /// 执行单个下载任务。
    /// </summary>
    private async Task ProcessQueuedDownloadAsync(QueuedPluginDownloadRequest request)
    {
        var downloadSessionPath = Path.Combine(
            AppConstants.AppTempPath,
            "PluginMarket",
            request.QueueItem.PluginId,
            request.QueueItem.QueueId);
        var tempZipPath = Path.Combine(downloadSessionPath, "package.zip");
        var extractPath = Path.Combine(downloadSessionPath, "extract");
        var downloadService = CreateDownloadService();

        if (Directory.Exists(downloadSessionPath))
        {
            Directory.Delete(downloadSessionPath, true);
        }
        Directory.CreateDirectory(downloadSessionPath);

        CancellationTokenSource localCts;
        lock (_downloadLock)
        {
            IsDownloading = true;
            IsDownloadFinished = _completedDownloadResults.Count > 0;
            DownloadProgress = 0;
            DownloadBytesPerSecond = 0;
            CurrentDownloadPluginId = request.QueueItem.PluginId;
            _currentDownloader = downloadService;
            _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
            localCts = _downloadCts;
        }

        UpdateQueueItem(request.QueueItem, queueItem =>
        {
            queueItem.Status = PluginDownloadQueueStatus.QueueDownloading;
            queueItem.CanCancel = true;
            queueItem.Progress = 0;
            queueItem.ProgressText = "0.00%";
            queueItem.SpeedText = string.Empty;
            queueItem.ErrorMessage = string.Empty;
        });

        RaiseDownloadStateChanged();

        try
        {
            await downloadService.DownloadFileTaskAsync(request.Item.ResolvedDownloadUrl, tempZipPath);
            localCts.Token.ThrowIfCancellationRequested();
            await EnsureDownloadedZipReadyAsync(tempZipPath, localCts.Token);

            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(tempZipPath, extractPath, true);

            var result = new PluginPackageDownloadResult
            {
                ExtractedDirectoryPath = extractPath
            };

            lock (_downloadLock)
            {
                _completedDownloadResults.Enqueue(result);
                IsDownloadFinished = true;
            }

            UpdateQueueItem(request.QueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueDownloaded;
                queueItem.CanCancel = false;
                queueItem.Progress = 100;
                queueItem.ProgressText = "100.00%";
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = string.Empty;
            });
        }
        catch (OperationCanceledException)
        {
            lock (_downloadLock)
            {
                IsDownloadFinished = _completedDownloadResults.Count > 0;
            }
            CleanupDownloadArtifacts(downloadSessionPath);
            UpdateQueueItem(request.QueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueCanceled;
                queueItem.CanCancel = false;
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = string.Empty;
            });
        }
        catch (Exception ex)
        {
            lock (_downloadLock)
            {
                IsDownloadFinished = _completedDownloadResults.Count > 0;
            }
            CleanupDownloadArtifacts(downloadSessionPath);
            UpdateQueueItem(request.QueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueFailed;
                queueItem.CanCancel = false;
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = ex.Message;
            });
            _logger.LogError(ex, "Error downloading plugin package for {PluginId}", request.QueueItem.PluginId);
        }
        finally
        {
            lock (_downloadLock)
            {
                IsDownloading = false;
                DownloadProgress = 0;
                DownloadBytesPerSecond = 0;
                CurrentDownloadPluginId = string.Empty;
                _currentDownloader = null;
                _currentDownloadRequest = null;
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

    /// <summary>
    /// 在界面线程中执行指定操作。
    /// </summary>
    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Application.Current.Dispatcher.Invoke(action);
    }

    /// <summary>
    /// 更新单个下载任务的显示状态。
    /// </summary>
    private static void UpdateQueueItem(PluginDownloadQueueItem queueItem, Action<PluginDownloadQueueItem> updateAction)
    {
        RunOnUiThread(() => updateAction(queueItem));
    }

    /// <summary>
    /// 表示一个待处理的下载请求。
    /// </summary>
    private sealed record QueuedPluginDownloadRequest(
        PluginMarketItem Item,
        PluginDownloadQueueItem QueueItem,
        CancellationToken CancellationToken);

    /// <summary>
    /// 等待下载的压缩包可以被正常读取。
    /// </summary>
    private static async Task EnsureDownloadedZipReadyAsync(string zipPath, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(zipPath))
            {
                try
                {
                    var fileInfo = new FileInfo(zipPath);
                    if (fileInfo.Length > 0)
                    {
                        using var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                        _ = archive.Entries.Count;
                        return;
                    }
                }
                catch (IOException)
                {
                }
                catch (InvalidDataException)
                {
                }
            }

            await Task.Delay(150, cancellationToken);
        }

        throw new IOException($"Downloaded plugin package is missing or incomplete: {zipPath}");
    }

    /// <summary>
    /// 将 README 中的相对链接改写为绝对链接。
    /// </summary>
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

    /// <summary>
    /// 重写 Markdown 链接目标。
    /// </summary>
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

    /// <summary>
    /// 重写 HTML 属性中的链接目标。
    /// </summary>
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

    /// <summary>
    /// 将相对链接解析为绝对链接。
    /// </summary>
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
