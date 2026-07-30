using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 为 SmartBP 托管资源统一构造下载请求。
/// </summary>
internal static class SmartBpParallelDownload
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    /// <summary>
    /// 使用宿主统一下载服务下载单个文件。
    /// </summary>
    /// <param name="downloadService">统一文件下载服务。</param>
    /// <param name="sourceUrl">资源下载地址。</param>
    /// <param name="destinationFilePath">最终文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="progressChanged">可选的下载进度回调。</param>
    /// <param name="operationChanged">创建操作后用于保存当前操作的回调。</param>
    /// <returns>下载完成任务。</returns>
    internal static async Task DownloadFileAsync(
        IFileDownloadService downloadService,
        string sourceUrl,
        string destinationFilePath,
        CancellationToken cancellationToken,
        Action<FileDownloadProgress>? progressChanged = null,
        Action<IFileDownloadOperation?>? operationChanged = null)
    {
        var uri = new Uri(sourceUrl, UriKind.Absolute);
        var operation = downloadService.CreateDownload(new FileDownloadRequest(uri, destinationFilePath)
        {
            UserAgent = BrowserUserAgent,
            Referer = new Uri(uri.GetLeftPart(UriPartial.Authority) + "/")
        });
        operation.StateChanged += OnStateChanged;
        operationChanged?.Invoke(operation);
        try
        {
            await operation.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operation.StateChanged -= OnStateChanged;
            operationChanged?.Invoke(null);
        }

        void OnStateChanged(object? sender, EventArgs args) =>
            progressChanged?.Invoke(operation.Progress);
    }
}
