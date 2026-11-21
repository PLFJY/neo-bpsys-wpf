using neo_bpsys_wpf.Models.Plugins;

namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>保存目录元数据，不保存安装状态、下载任务或 README 呈现状态。</summary>
public interface IPluginMarketCatalogCache
{
    /// <summary>异步读取指定上下文的最后完整目录，不发起网络或镜像探测。</summary>
    /// <param name="context">目录上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存目录（允许为空）；不存在或不可用时为 null。</returns>
    /// <exception cref="OperationCanceledException">读取已取消。</exception>
    Task<IReadOnlyList<PluginMarketItem>?> ReadAsync(PluginMarketCatalogContext context, CancellationToken cancellationToken = default);

    /// <summary>异步原子保存完整目录，写入失败时保留上一次缓存并记录日志。</summary>
    /// <param name="context">目录上下文。</param>
    /// <param name="items">已获取且完整的目录条目。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保存完成的任务。</returns>
    /// <exception cref="OperationCanceledException">写入已取消。</exception>
    Task WriteAsync(PluginMarketCatalogContext context, IReadOnlyList<PluginMarketItem> items, CancellationToken cancellationToken = default);
}
