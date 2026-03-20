using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>
/// 表示插件市场中的一个下载任务。
/// </summary>
public partial class PluginDownloadQueueItem : ObservableObjectBase
{
    /// <summary>
    /// 下载任务唯一标识。
    /// </summary>
    public string QueueId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 插件 ID。
    /// </summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>
    /// 插件名称。
    /// </summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// 插件版本号。
    /// </summary>
    public string PluginVersion { get; init; } = string.Empty;

    /// <summary>
    /// 当前下载状态。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusKey))]
    [NotifyPropertyChangedFor(nameof(IsInProgress))]
    private PluginDownloadQueueStatus _status = PluginDownloadQueueStatus.QueuePending;

    /// <summary>
    /// 当前下载状态对应的本地化 Key。
    /// </summary>
    public string StatusKey => Status.ToString();

    /// <summary>
    /// 当前任务是否仍处于进行中。
    /// </summary>
    public bool IsInProgress =>
        Status is PluginDownloadQueueStatus.QueuePending or PluginDownloadQueueStatus.QueueDownloading;

    /// <summary>
    /// 当前下载进度，范围 0-100。
    /// </summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>
    /// 当前进度文本。
    /// </summary>
    [ObservableProperty]
    private string _progressText = string.Empty;

    /// <summary>
    /// 当前下载速度文本。
    /// </summary>
    [ObservableProperty]
    private string _speedText = string.Empty;

    /// <summary>
    /// 下载失败时显示的错误信息。
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 当前任务是否允许取消。
    /// </summary>
    [ObservableProperty]
    private bool _canCancel = true;
}
