using System.ComponentModel;
using System.Runtime.ExceptionServices;
using Downloader;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 为 SmartBP 托管资源创建统一的并行分片下载器。
/// </summary>
internal static class SmartBpParallelDownload
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    /// <summary>
    /// 创建采用更新器同等级并发和重试策略的下载服务。
    /// </summary>
    /// <param name="downloadUri">当前下载地址，用于设置请求来源。</param>
    /// <returns>新的下载服务；调用方负责释放。</returns>
    internal static DownloadService CreateService(Uri downloadUri)
    {
        return new DownloadService(CreateConfiguration(downloadUri));
    }

    /// <summary>
    /// 创建统一的并行下载配置。
    /// </summary>
    /// <param name="downloadUri">当前下载地址，用于设置请求来源。</param>
    /// <returns>下载器配置。</returns>
    internal static DownloadConfiguration CreateConfiguration(Uri downloadUri)
    {
        return new DownloadConfiguration
        {
            ChunkCount = 8,
            ParallelDownload = true,
            ParallelCount = 6,
            MaxTryAgainOnFailure = 5,
            EnableAutoResumeDownload = true,
            MaximumMemoryBufferBytes = 50 * 1024 * 1024,
            CheckDiskSizeBeforeDownload = true,
            RequestConfiguration =
            {
                Accept = "application/octet-stream, */*;q=0.8",
                KeepAlive = true,
                UserAgent = BrowserUserAgent,
                Referer = downloadUri.GetLeftPart(UriPartial.Authority) + "/"
            }
        };
    }

    /// <summary>
    /// 使用并行分片、自动续传和重试策略下载单个文件。
    /// </summary>
    /// <param name="sourceUrl">资源下载地址。</param>
    /// <param name="destinationFilePath">最终文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="progressChanged">可选的下载进度回调。</param>
    /// <returns>下载完成任务。</returns>
    /// <exception cref="OperationCanceledException">下载被取消。</exception>
    /// <exception cref="Exception">下载器报告下载失败时，重新抛出原始异常。</exception>
    internal static async Task DownloadFileAsync(
        string sourceUrl,
        string destinationFilePath,
        CancellationToken cancellationToken,
        Action<DownloadProgressChangedEventArgs>? progressChanged = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var downloadUri = new Uri(sourceUrl, UriKind.Absolute);
        await using var downloader = CreateService(downloadUri);
        var completion = new TaskCompletionSource<AsyncCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        downloader.DownloadProgressChanged += OnProgressChanged;
        downloader.DownloadFileCompleted += OnDownloadCompleted;
        try
        {
            await downloader.DownloadFileTaskAsync(sourceUrl, destinationFilePath, cancellationToken)
                .ConfigureAwait(false);
            var result = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (result.Cancelled)
                throw new OperationCanceledException("The download was cancelled.", cancellationToken);
            if (result.Error is not null)
                ExceptionDispatchInfo.Capture(result.Error).Throw();
        }
        finally
        {
            downloader.DownloadProgressChanged -= OnProgressChanged;
            downloader.DownloadFileCompleted -= OnDownloadCompleted;
        }

        void OnProgressChanged(object? sender, DownloadProgressChangedEventArgs args) =>
            progressChanged?.Invoke(args);

        void OnDownloadCompleted(object? sender, AsyncCompletedEventArgs args) =>
            completion.TrySetResult(args);
    }
}
