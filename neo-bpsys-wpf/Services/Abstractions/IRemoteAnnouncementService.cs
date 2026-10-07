using neo_bpsys_wpf.Models.RemoteAnnouncements;

namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>本机公告缓存、已读状态及启动时远程同步。</summary>
public interface IRemoteAnnouncementService
{
    /// <summary>当前适用且缓存有效的公告，按发布时间倒序排列。</summary>
    IReadOnlyList<RemoteAnnouncement> Announcements { get; }

    /// <summary>已读公告 ID。</summary>
    IReadOnlyCollection<string> SeenAnnouncementIds { get; }

    /// <summary>未读公告数。</summary>
    int UnseenCount { get; }

    /// <summary>本次启动的远程检查（包括离线降级）是否已经结束。</summary>
    bool IsStartupCheckCompleted { get; }

    /// <summary>公告列表或已读状态改变时触发；调用方需自行切回 UI 线程。</summary>
    event EventHandler? AnnouncementsChanged;

    /// <summary>异步加载本地缓存。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载任务。</returns>
    Task InitializeFromCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>每次进程启动最多执行一次远程同步。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>同步任务；普通网络故障在服务内记录并降级。</returns>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>用户从公告中心发起一次即时远程同步。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>同步任务；普通网络故障在服务内记录并降级。</returns>
    Task RefreshOnDemandAsync(CancellationToken cancellationToken = default);

    /// <summary>将当前可显示公告的 ID 标记为已读并保存。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>持久化任务。</returns>
    Task MarkAllCurrentAsSeenAsync(CancellationToken cancellationToken = default);
}
