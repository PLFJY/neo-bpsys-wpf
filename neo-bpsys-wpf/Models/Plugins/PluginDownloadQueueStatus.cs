namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>
/// 表示插件下载任务当前所处的阶段。
/// </summary>
public enum PluginDownloadQueueStatus
{
    /// <summary>
    /// 任务已加入队列，等待开始下载。
    /// </summary>
    QueuePending,

    /// <summary>
    /// 任务正在下载中。
    /// </summary>
    QueueDownloading,

    /// <summary>
    /// 插件包已下载并解压完成，等待安装。
    /// </summary>
    QueueDownloaded,

    /// <summary>
    /// 插件已经安装完成，等待重启后应用更改。
    /// </summary>
    QueueInstalledRestartRequired,

    /// <summary>
    /// 任务下载失败。
    /// </summary>
    QueueFailed,

    /// <summary>
    /// 任务已被用户取消。
    /// </summary>
    QueueCanceled
}
