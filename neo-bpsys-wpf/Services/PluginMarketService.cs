using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using neo_bpsys_wpf.Core.Models.Archives;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 插件市场服务实现。
/// </summary>
public class PluginMarketService : IPluginMarketService
{
    private const string DefaultMarketIndexUrl =
        "https://bpsys-plugin-index.plfjy.top/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PluginMarketService> _logger;
    private static ILogger<PluginMarketService>? StaticLogger => IAppHost.TryGetService<ILogger<PluginMarketService>>();
    private readonly ISettingsHostService _settingsHostService;
    private readonly IArchiveService _archiveService;
    private readonly IFileDownloadService _fileDownloadService;
    private readonly Lock _downloadLock = new();
    private CancellationTokenSource? _downloadCts;
    private IFileDownloadOperation? _currentDownload;
    private QueuedPluginDownloadRequest? _currentDownloadRequest;
    private readonly ObservableCollection<PluginDownloadQueueItem> _downloadQueueInternal = [];
    private readonly Queue<QueuedPluginDownloadRequest> _pendingDownloads = new();
    private readonly IGitHubDownloadUrlResolver _githubDownloadUrlResolver;
    private readonly Queue<PluginPackageDownloadResult> _completedDownloadResults = new();
    private bool _isProcessingQueue;

    /// <summary>
    /// 初始化插件市场服务。
    /// </summary>
    public PluginMarketService(
        ILogger<PluginMarketService> logger,
        ISettingsHostService settingsHostService,
        IArchiveService archiveService,
        IGitHubDownloadUrlResolver githubDownloadUrlResolver,
        IFileDownloadService fileDownloadService)
    {
        _logger = logger;
        _settingsHostService = settingsHostService;
        _archiveService = archiveService;
        _githubDownloadUrlResolver = githubDownloadUrlResolver;
        _fileDownloadService = fileDownloadService;
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
    /// 当前插件下载是否已暂停。
    /// </summary>
    public bool IsDownloadPaused { get; private set; }

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
        var marketIndexUrl = GetCurrentMarketIndexUrl();
        var response = await _httpClient.GetAsync(await ResolveGitHubUrlAsync(marketIndexUrl, cancellationToken),
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
            value.Name = string.IsNullOrWhiteSpace(value.Name) ? value.Id : value.Name;
            value.Description ??= string.Empty;
            value.Author ??= string.Empty;
            value.Icon ??= string.Empty;
            value.Readme ??= string.Empty;
            value.Url ??= string.Empty;
            value.DownloadUrl ??= string.Empty;
            value.Sha256 ??= string.Empty;
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
        IFileDownloadOperation? currentDownload;
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
            currentDownload = _currentDownload;
        }

        currentDownload?.Cancel();
    }

    /// <summary>
    /// 取消指定下载任务。
    /// </summary>
    public void CancelDownload(string queueId)
    {
        IFileDownloadOperation? currentDownload = null;
        PluginDownloadQueueItem? canceledQueueItem = null;

        lock (_downloadLock)
        {
            if (_currentDownloadRequest?.QueueItem.QueueId == queueId)
            {
                _downloadCts?.Cancel();
                currentDownload = _currentDownload;
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

        if (canceledQueueItem != null && currentDownload == null)
        {
            UpdateQueueItem(canceledQueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueCanceled;
                queueItem.CanCancel = false;
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = string.Empty;
            });
        }

        currentDownload?.Cancel();
        RaiseDownloadStateChanged();
    }

    /// <inheritdoc />
    public void PauseDownload()
    {
        IFileDownloadOperation? operation;
        lock (_downloadLock)
            operation = _currentDownload;
        operation?.Pause();
    }

    /// <inheritdoc />
    public void ResumeDownload()
    {
        IFileDownloadOperation? operation;
        lock (_downloadLock)
            operation = _currentDownload;
        operation?.Resume();
    }

    /// <inheritdoc />
    public void PauseDownload(string queueId)
    {
        IFileDownloadOperation? operation = null;
        lock (_downloadLock)
        {
            if (_currentDownloadRequest?.QueueItem.QueueId == queueId)
                operation = _currentDownload;
        }

        operation?.Pause();
    }

    /// <inheritdoc />
    public void ResumeDownload(string queueId)
    {
        IFileDownloadOperation? operation = null;
        lock (_downloadLock)
        {
            if (_currentDownloadRequest?.QueueItem.QueueId == queueId)
                operation = _currentDownload;
        }

        operation?.Resume();
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
        _githubDownloadUrlResolver.ResetCache();
    }

    /// <summary>
    /// 解析插件市场和插件包实际使用的下载地址。
    /// </summary>
    private async Task<string> ResolveGitHubUrlAsync(string url, CancellationToken cancellationToken)
    {
        return await _githubDownloadUrlResolver.ResolveAsync(url, cancellationToken);
    }

    /// <summary>
    /// 获取当前插件市场索引地址。
    /// 当设置文件中没有保存插件源时，回退到内置默认源。
    /// </summary>
    /// <returns>当前实际使用的插件市场索引地址。</returns>
    private string GetCurrentMarketIndexUrl()
    {
        return string.IsNullOrWhiteSpace(_settingsHostService.Settings.PluginMarketSource)
            ? DefaultMarketIndexUrl
            : _settingsHostService.Settings.PluginMarketSource;
    }

    /// <summary>
    /// 更新当前下载进度和速度。
    /// </summary>
    private void OnCurrentDownloadStateChanged(object? sender, EventArgs e)
    {
        if (sender is not IFileDownloadOperation operation)
            return;
        var progress = operation.Progress;
        lock (_downloadLock)
        {
            DownloadProgress = progress.Percentage ?? 0;
            DownloadBytesPerSecond = progress.BytesPerSecond;
            IsDownloadPaused = operation.State == FileDownloadState.Paused;
        }

        var currentRequest = _currentDownloadRequest;
        if (currentRequest != null)
        {
            UpdateQueueItem(currentRequest.QueueItem, queueItem =>
            {
                queueItem.Progress = progress.Percentage ?? 0;
                queueItem.ProgressText = progress.Percentage is { } percentage
                    ? $"{percentage:0.00}%"
                    : string.Empty;
                queueItem.SpeedText = operation.State == FileDownloadState.Paused
                    ? string.Empty
                    : $"{(progress.BytesPerSecond / 1024 / 1024):0.00} MB/s";
                queueItem.Status = operation.State == FileDownloadState.Paused
                    ? PluginDownloadQueueStatus.QueuePaused
                    : PluginDownloadQueueStatus.QueueDownloading;
                queueItem.CanPause = operation.State == FileDownloadState.Downloading;
                queueItem.CanResume = operation.State == FileDownloadState.Paused;
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
            "Sessions",
            request.QueueItem.PluginId,
            request.QueueItem.QueueId);
        var downloadCacheKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{request.Item.Id}\n{request.Item.Version}\n{request.Item.Sha256}\n{request.Item.ResolvedDownloadUrl}")));
        var downloadCachePath = Path.Combine(
            AppConstants.AppTempPath,
            "PluginMarket",
            "Downloads",
            downloadCacheKey);
        var tempArchivePath = Path.Combine(downloadCachePath, "package.archive");
        var extractPath = Path.Combine(downloadSessionPath, "extract");

        if (Directory.Exists(downloadSessionPath))
        {
            Directory.Delete(downloadSessionPath, true);
        }
        Directory.CreateDirectory(downloadSessionPath);
        Directory.CreateDirectory(downloadCachePath);
        var download = _fileDownloadService.CreateDownload(new FileDownloadRequest(
            new Uri(request.Item.ResolvedDownloadUrl, UriKind.Absolute),
            tempArchivePath)
        {
            UserAgent = AppConstants.AppName
        });
        download.StateChanged += OnCurrentDownloadStateChanged;

        lock (_downloadLock)
        {
            IsDownloading = true;
            IsDownloadFinished = _completedDownloadResults.Count > 0;
            DownloadProgress = 0;
            DownloadBytesPerSecond = 0;
            CurrentDownloadPluginId = request.QueueItem.PluginId;
            _currentDownload = download;
            _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        }

        UpdateQueueItem(request.QueueItem, queueItem =>
        {
            queueItem.Status = PluginDownloadQueueStatus.QueueDownloading;
            queueItem.CanCancel = true;
            queueItem.CanPause = true;
            queueItem.CanResume = false;
            queueItem.Progress = 0;
            queueItem.ProgressText = "0.00%";
            queueItem.SpeedText = string.Empty;
            queueItem.ErrorMessage = string.Empty;
        });

        RaiseDownloadStateChanged();

        try
        {
            await download.StartAsync(_downloadCts.Token);
            await EnsureDownloadedArchiveReadyAsync(tempArchivePath, _downloadCts.Token);
            ValidateDownloadedPackageHash(request.Item, tempArchivePath);

            Directory.CreateDirectory(extractPath);
            UpdateQueueItem(request.QueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueExtracting;
                queueItem.CanPause = false;
                queueItem.CanResume = false;
                queueItem.Progress = 100;
                queueItem.ProgressText = "100.00%";
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = string.Empty;
            });
            var extractionProgress = new Progress<ArchiveProgress>(progress =>
            {
                UpdateQueueItem(request.QueueItem, queueItem =>
                {
                    queueItem.Progress = progress.Percentage;
                    queueItem.ProgressText = $"{progress.Percentage:0.00}%";
                });
            });
            await _archiveService.ExtractToDirectoryAsync(
                tempArchivePath,
                extractPath,
                extractionProgress,
                _downloadCts.Token);

            var result = new PluginPackageDownloadResult
            {
                ExtractedDirectoryPath = extractPath,
                QueueItem = request.QueueItem
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
                queueItem.CanPause = false;
                queueItem.CanResume = false;
                queueItem.Progress = 100;
                queueItem.ProgressText = "100.00%";
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = string.Empty;
            });
        }
        catch (OperationCanceledException)
        {
            lock (_downloadLock)
                IsDownloadFinished = _completedDownloadResults.Count > 0;
            CleanupDownloadArtifacts(downloadSessionPath);
            UpdateQueueItem(request.QueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueCanceled;
                queueItem.CanCancel = false;
                queueItem.CanPause = false;
                queueItem.CanResume = false;
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = string.Empty;
            });
        }
        catch (Exception ex)
        {
            lock (_downloadLock)
                IsDownloadFinished = _completedDownloadResults.Count > 0;
            CleanupDownloadArtifacts(downloadSessionPath);
            if (File.Exists(tempArchivePath))
                File.Delete(tempArchivePath);
            UpdateQueueItem(request.QueueItem, queueItem =>
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueFailed;
                queueItem.CanCancel = false;
                queueItem.CanPause = false;
                queueItem.CanResume = false;
                queueItem.SpeedText = string.Empty;
                queueItem.ErrorMessage = ex.Message;
            });
            _logger.LogError(ex, "Error downloading plugin package for {PluginId}", request.QueueItem.PluginId);
        }
        finally
        {
            download.StateChanged -= OnCurrentDownloadStateChanged;
            lock (_downloadLock)
            {
                IsDownloading = false;
                IsDownloadPaused = false;
                DownloadProgress = 0;
                DownloadBytesPerSecond = 0;
                CurrentDownloadPluginId = string.Empty;
                _currentDownload = null;
                _currentDownloadRequest = null;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }

            if (File.Exists(tempArchivePath))
            {
                File.Delete(tempArchivePath);
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
    private async Task EnsureDownloadedArchiveReadyAsync(string archivePath, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(archivePath))
            {
                try
                {
                    var fileInfo = new FileInfo(archivePath);
                    if (fileInfo.Length > 0)
                    {
                        await _archiveService.DetectFormatAsync(archivePath, cancellationToken);
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

        throw new IOException($"Downloaded plugin package is missing or incomplete: {archivePath}");
    }

    /// <summary>
    /// 校验下载完成的插件压缩包是否与插件市场声明的 SHA-256 一致。
    /// 校验发生在解压之前，这样一旦发现压缩包被篡改或损坏，就可以直接中断流程并清理整个下载会话目录，
    /// 避免任何不可信内容进入后续安装步骤。
    /// </summary>
    /// <param name="item">当前下载的插件市场条目。</param>
    /// <param name="archivePath">已经下载完成的插件压缩包路径。</param>
    /// <exception cref="InvalidOperationException">
    /// 当压缩包的 SHA-256 与插件市场声明值不一致时抛出。
    /// </exception>
    private static void ValidateDownloadedPackageHash(PluginMarketItem item, string archivePath)
    {
        if (string.IsNullOrWhiteSpace(item.Sha256))
        {
            return;
        }

        var expectedHash = NormalizeSha256(item.Sha256);
        var actualHash = ComputeFileSha256(archivePath);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            StaticLogger?.LogError("SHA-256 mismatch for plugin {PluginName}", FormatPluginDisplayName(item));
            throw new InvalidOperationException(
                string.Format(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginMarketSha256Mismatch"),
                    FormatPluginDisplayName(item)));
        }
    }

    /// <summary>
    /// 计算指定文件的 SHA-256，并返回连续的小写十六进制字符串。
    /// 这里直接读取已经落盘的归档文件，确保比较的是最终下载结果，而不是下载器过程中的中间数据。
    /// </summary>
    /// <param name="filePath">待计算哈希的文件路径。</param>
    /// <returns>文件内容对应的 SHA-256。</returns>
    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// 规范化 SHA-256 文本。
    /// 允许配置中出现大小写混合或带连字符的写法，比较前统一转成连续的小写十六进制字符串。
    /// </summary>
    /// <param name="value">原始 SHA-256 文本。</param>
    /// <returns>规范化后的 SHA-256。</returns>
    private static string NormalizeSha256(string value)
    {
        return value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    /// <summary>
    /// 生成用于展示给用户的插件名称。
    /// 显示格式固定为带双引号的“插件名[插件ID]”，避免只显示名称时难以区分同名插件。
    /// </summary>
    /// <param name="item">插件市场条目。</param>
    /// <returns>用于提示信息的插件显示名称。</returns>
    private static string FormatPluginDisplayName(PluginMarketItem item)
    {
        return $"\"{item.Name}[{item.Id}]\"";
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
