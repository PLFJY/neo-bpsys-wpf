using neo_bpsys_wpf.Models.Plugins;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>
/// 插件市场服务。
/// </summary>
public interface IPluginMarketService
{
    /// <summary>
    /// 当前插件下载队列。
    /// </summary>
    ReadOnlyObservableCollection<PluginDownloadQueueItem> DownloadQueue { get; }

    /// <summary>
    /// 当前是否正在下载插件。
    /// </summary>
    bool IsDownloading { get; }

    /// <summary>
    /// 当前是否存在已下载完成、等待安装的插件包。
    /// </summary>
    bool IsDownloadFinished { get; }

    /// <summary>
    /// 当前下载进度，范围 0-100。
    /// </summary>
    double DownloadProgress { get; }

    /// <summary>
    /// 当前下载速度，单位为字节/秒。
    /// </summary>
    double DownloadBytesPerSecond { get; }

    /// <summary>
    /// 当前正在下载的插件 ID。
    /// </summary>
    string CurrentDownloadPluginId { get; }

    /// <summary>
    /// 下载状态变化事件。
    /// </summary>
    event EventHandler? DownloadStateChanged;

    /// <summary>
    /// 获取插件市场中的插件列表。
    /// </summary>
    Task<IReadOnlyList<PluginMarketItem>> GetMarketPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定插件的 README Markdown 内容。
    /// </summary>
    Task<string> GetReadmeMarkdownAsync(PluginMarketItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将指定插件加入下载队列。
    /// </summary>
    Task<bool> QueuePluginDownloadAsync(PluginMarketItem item,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取出一个已下载完成、等待安装的插件包。
    /// </summary>
    PluginPackageDownloadResult? ConsumeCompletedDownload();

    /// <summary>
    /// 取消当前正在下载的任务。
    /// </summary>
    void CancelDownload();

    /// <summary>
    /// 按队列项 ID 取消指定下载任务。
    /// </summary>
    void CancelDownload(string queueId);

    /// <summary>
    /// 清空镜像缓存。
    /// </summary>
    void ResetMirrorCache();
}
