using System.IO;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 创建支持暂停、取消和断点续传的文件下载操作。
/// </summary>
public interface IFileDownloadService
{
    /// <summary>
    /// 创建一个尚未启动的文件下载操作。
    /// </summary>
    /// <param name="request">下载请求。</param>
    /// <returns>可由调用方启动和控制的下载操作。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 为 <see langword="null"/>。</exception>
    IFileDownloadOperation CreateDownload(FileDownloadRequest request);
}

/// <summary>
/// 表示一个可暂停和恢复的文件下载操作。
/// </summary>
public interface IFileDownloadOperation
{
    /// <summary>
    /// 获取下载请求。
    /// </summary>
    FileDownloadRequest Request { get; }

    /// <summary>
    /// 获取当前操作状态。
    /// </summary>
    FileDownloadState State { get; }

    /// <summary>
    /// 获取下载过程中最后一次报告的进度。
    /// </summary>
    FileDownloadProgress Progress { get; }

    /// <summary>
    /// 获取导致下载失败的异常；其他状态下为 <see langword="null"/>。
    /// </summary>
    Exception? Error { get; }

    /// <summary>
    /// 下载状态或进度变化时触发。
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// 启动下载并等待下载完成。
    /// 暂停期间此任务保持未完成，恢复后继续传输。
    /// </summary>
    /// <param name="cancellationToken">用于取消整个操作的令牌。</param>
    /// <returns>下载完成任务。</returns>
    /// <exception cref="InvalidOperationException">操作已经启动。</exception>
    /// <exception cref="OperationCanceledException">操作被取消。</exception>
    /// <exception cref="HttpRequestException">服务端返回失败状态或网络请求失败。</exception>
    /// <exception cref="IOException">文件读写失败或响应长度不完整。</exception>
    /// <exception cref="TimeoutException">单次请求超过配置的超时时间。</exception>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停当前网络传输并保留部分文件。
    /// </summary>
    void Pause();

    /// <summary>
    /// 恢复已暂停的网络传输。
    /// </summary>
    void Resume();

    /// <summary>
    /// 取消本次操作并保留可供后续新操作续传的部分文件。
    /// </summary>
    void Cancel();
}

/// <summary>
/// 文件下载操作状态。
/// </summary>
public enum FileDownloadState
{
    /// <summary>尚未启动。</summary>
    Pending,
    /// <summary>正在传输。</summary>
    Downloading,
    /// <summary>已暂停，部分文件被保留。</summary>
    Paused,
    /// <summary>已完成。</summary>
    Completed,
    /// <summary>已取消，部分文件被保留。</summary>
    Canceled,
    /// <summary>下载失败。</summary>
    Failed
}

/// <summary>
/// 文件下载请求。
/// </summary>
public sealed class FileDownloadRequest
{
    /// <summary>
    /// 初始化文件下载请求。
    /// </summary>
    /// <param name="sourceUri">源文件的绝对 HTTP 或 HTTPS URI。</param>
    /// <param name="destinationFilePath">最终文件路径。</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceUri"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">URI 不是 HTTP/HTTPS 绝对地址，或目标路径为空。</exception>
    public FileDownloadRequest(Uri sourceUri, string destinationFilePath)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        if (!sourceUri.IsAbsoluteUri || sourceUri.Scheme is not ("http" or "https"))
            throw new ArgumentException("The download URI must be an absolute HTTP or HTTPS URI.", nameof(sourceUri));
        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("The destination file path is required.", nameof(destinationFilePath));

        SourceUri = sourceUri;
        DestinationFilePath = Path.GetFullPath(destinationFilePath);
    }

    /// <summary>
    /// 获取源文件 URI。
    /// </summary>
    public Uri SourceUri { get; }

    /// <summary>
    /// 获取最终文件路径。
    /// </summary>
    public string DestinationFilePath { get; }

    /// <summary>
    /// 获取或设置单次请求超时。默认为 15 分钟。
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// 获取或设置瞬时网络错误后的最大重试次数。默认为 5 次。
    /// </summary>
    public int MaxRetries { get; init; } = 5;

    /// <summary>
    /// 获取或设置请求 User-Agent。
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// 获取或设置请求 Referer。
    /// </summary>
    public Uri? Referer { get; init; }

    /// <summary>
    /// 获取要附加到下载请求的其他 HTTP 标头。
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 文件下载进度快照。
/// </summary>
/// <param name="BytesReceived">当前部分文件中已有的字节数。</param>
/// <param name="TotalBytes">服务端声明的完整文件大小；未知时为 <see langword="null"/>。</param>
/// <param name="BytesPerSecond">当前传输的估算速度。</param>
/// <param name="Percentage">下载百分比；总大小未知时为 <see langword="null"/>。</param>
/// <param name="IsResumed">本次 HTTP 传输是否从已有部分文件续传。</param>
public sealed record FileDownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double BytesPerSecond,
    double? Percentage,
    bool IsResumed)
{
    /// <summary>
    /// 获取尚未收到任何数据时的默认进度。
    /// </summary>
    public static FileDownloadProgress Empty { get; } = new(0, null, 0, null, false);
}
