using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using Downloader;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Services;

/// <summary>
/// 基于 <c>Downloader</c> 提供多分片下载、暂停、自动断点续传和原生进度回调的统一文件下载服务。
/// </summary>
public sealed class FileDownloadService : IFileDownloadService
{
    private const string PartialFileExtension = ".download.part";
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _destinationLocks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 初始化文件下载服务。
    /// </summary>
    /// <param name="httpClientFactory">为每个 Downloader 网络客户端创建 HTTP 客户端的工厂。</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClientFactory"/> 为 <see langword="null"/>。</exception>
    public FileDownloadService(Func<HttpClient> httpClientFactory)
    {
        _httpClientFactory =
            httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc />
    public IFileDownloadOperation CreateDownload(FileDownloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new FileDownloadOperation(this, request);
    }

    private HttpClient CreateHttpClient() =>
        _httpClientFactory()
        ?? throw new InvalidOperationException("The HTTP client factory returned null.");

    private DownloadConfiguration CreateConfiguration(FileDownloadRequest request)
    {
        var headers = new WebHeaderCollection();
        foreach (var header in request.Headers)
            headers[header.Key] = header.Value;

        var timeoutMilliseconds = request.RequestTimeout == Timeout.InfiniteTimeSpan
            ? Timeout.Infinite
            : (int)Math.Clamp(request.RequestTimeout.TotalMilliseconds, 1, int.MaxValue);
        var configuration = new DownloadConfiguration
        {
            ChunkCount = 8,
            ParallelDownload = true,
            ParallelCount = 6,
            MaxTryAgainOnFailure = Math.Max(0, request.MaxRetries),
            EnableAutoResumeDownload = true,
            DownloadFileExtension = PartialFileExtension,
            ClearPackageOnCompletionWithFailure = false,
            FileExistPolicy = FileExistPolicy.Delete,
            HttpClientTimeout = timeoutMilliseconds,
            CustomHttpClientFactory = CreateHttpClient,
            RequestConfiguration = new RequestConfiguration
            {
                Accept = "*/*",
                Headers = headers,
                KeepAlive = true,
                UserAgent = request.UserAgent,
                Referer = request.Referer?.AbsoluteUri
            }
        };
        return configuration;
    }

    private async Task RunDownloadAsync(
        FileDownloadOperation operation,
        CancellationToken cancellationToken)
    {
        var destinationLock = _destinationLocks.GetOrAdd(
            operation.Request.DestinationFilePath,
            static _ => new SemaphoreSlim(1, 1));
        await destinationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation.RunDownloaderAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            destinationLock.Release();
        }
    }

    private sealed class FileDownloadOperation : IFileDownloadOperation
    {
        private readonly FileDownloadService _owner;
        private readonly object _stateLock = new();
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private DownloadService? _downloader;
        private Task? _executionTask;
        private FileDownloadState _state = FileDownloadState.Pending;
        private FileDownloadProgress _progress = FileDownloadProgress.Empty;
        private Exception? _error;
        private bool _isResumed;

        internal FileDownloadOperation(FileDownloadService owner, FileDownloadRequest request)
        {
            _owner = owner;
            Request = request;
        }

        /// <inheritdoc />
        public FileDownloadRequest Request { get; }

        /// <inheritdoc />
        public FileDownloadState State
        {
            get
            {
                lock (_stateLock)
                    return _state;
            }
        }

        /// <inheritdoc />
        public FileDownloadProgress Progress
        {
            get
            {
                lock (_stateLock)
                    return _progress;
            }
        }

        /// <inheritdoc />
        public Exception? Error
        {
            get
            {
                lock (_stateLock)
                    return _error;
            }
        }

        /// <inheritdoc />
        public event EventHandler? StateChanged;

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (_executionTask is not null)
                    throw new InvalidOperationException("The download operation has already been started.");

                Directory.CreateDirectory(Path.GetDirectoryName(Request.DestinationFilePath)!);
                _isResumed = File.Exists(Request.DestinationFilePath + PartialFileExtension);
                _downloader = new DownloadService(_owner.CreateConfiguration(Request));
                _downloader.DownloadProgressChanged += OnDownloadProgressChanged;
                _state = FileDownloadState.Downloading;
                _executionTask = RunAsync(cancellationToken);
            }

            RaiseStateChanged();
            return _executionTask;
        }

        /// <inheritdoc />
        public void Pause()
        {
            DownloadService? downloader;
            lock (_stateLock)
            {
                if (_state != FileDownloadState.Downloading)
                    return;
                _state = FileDownloadState.Paused;
                downloader = _downloader;
            }

            downloader?.Pause();
            RaiseStateChanged();
        }

        /// <inheritdoc />
        public void Resume()
        {
            DownloadService? downloader;
            lock (_stateLock)
            {
                if (_state != FileDownloadState.Paused)
                    return;
                _state = FileDownloadState.Downloading;
                downloader = _downloader;
            }

            downloader?.Resume();
            RaiseStateChanged();
        }

        /// <inheritdoc />
        public void Cancel()
        {
            DownloadService? downloader;
            lock (_stateLock)
            {
                if (_state is FileDownloadState.Completed or FileDownloadState.Canceled or FileDownloadState.Failed)
                    return;
                _lifetimeCancellation.Cancel();
                downloader = _downloader;
            }

            downloader?.CancelAsync();
        }

        internal async Task RunDownloaderAsync(CancellationToken cancellationToken)
        {
            DownloadService downloader;
            lock (_stateLock)
                downloader = _downloader!;

            AsyncCompletedEventArgs? completion = null;
            void OnCompleted(object? sender, AsyncCompletedEventArgs args) => completion = args;

            downloader.DownloadFileCompleted += OnCompleted;
            try
            {
                await DiscardPartialFileWhenRangeIsUnavailableAsync(cancellationToken)
                    .ConfigureAwait(false);
                using var cancellationRegistration =
                    cancellationToken.Register(static state => ((DownloadService)state!).CancelAsync(), downloader);
                await downloader.DownloadFileTaskAsync(
                    Request.SourceUri.AbsoluteUri,
                    Request.DestinationFilePath).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                if (completion?.Cancelled == true)
                    throw new OperationCanceledException(cancellationToken);
                if (completion?.Error is { } error)
                    throw error;
            }
            finally
            {
                downloader.DownloadFileCompleted -= OnCompleted;
                await downloader.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task DiscardPartialFileWhenRangeIsUnavailableAsync(
            CancellationToken cancellationToken)
        {
            bool isResumed;
            lock (_stateLock)
                isResumed = _isResumed;
            if (!isResumed)
                return;

            if (await ServerSupportsRangeAsync(cancellationToken).ConfigureAwait(false))
                return;

            File.Delete(Request.DestinationFilePath + PartialFileExtension);
            lock (_stateLock)
                _isResumed = false;
        }

        private async Task<bool> ServerSupportsRangeAsync(CancellationToken cancellationToken)
        {
            using var client = _owner.CreateHttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, Request.SourceUri);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            if (!string.IsNullOrWhiteSpace(Request.UserAgent))
                request.Headers.TryAddWithoutValidation("User-Agent", Request.UserAgent);
            if (Request.Referer is not null)
                request.Headers.Referrer = Request.Referer;
            foreach (var header in Request.Headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (Request.RequestTimeout != Timeout.InfiniteTimeSpan)
                timeoutCancellation.CancelAfter(Request.RequestTimeout);

            try
            {
                using var response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeoutCancellation.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return response.StatusCode == HttpStatusCode.PartialContent
                       && response.Content.Headers.ContentRange is not null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("The range-support probe timed out.");
            }
        }

        private async Task RunAsync(CancellationToken externalCancellation)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellation,
                _lifetimeCancellation.Token);
            try
            {
                await _owner.RunDownloadAsync(this, linkedCancellation.Token).ConfigureAwait(false);
                SetTerminalState(FileDownloadState.Completed, null);
            }
            catch (OperationCanceledException)
            {
                SetTerminalState(FileDownloadState.Canceled, null);
                throw;
            }
            catch (Exception ex)
            {
                SetTerminalState(FileDownloadState.Failed, ex);
                throw;
            }
            finally
            {
                DownloadService? downloader;
                lock (_stateLock)
                    downloader = _downloader;
                if (downloader is not null)
                    downloader.DownloadProgressChanged -= OnDownloadProgressChanged;
            }
        }

        private void OnDownloadProgressChanged(object? sender, Downloader.DownloadProgressChangedEventArgs args)
        {
            var totalBytes = args.TotalBytesToReceive > 0
                ? args.TotalBytesToReceive
                : (long?)null;
            lock (_stateLock)
            {
                _progress = new FileDownloadProgress(
                    args.ReceivedBytesSize,
                    totalBytes,
                    args.BytesPerSecondSpeed,
                    totalBytes.HasValue ? args.ProgressPercentage : null,
                    _isResumed);
            }

            RaiseStateChanged();
        }

        private void SetTerminalState(FileDownloadState state, Exception? error)
        {
            lock (_stateLock)
            {
                _state = state;
                _error = error;
            }

            RaiseStateChanged();
        }

        private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
