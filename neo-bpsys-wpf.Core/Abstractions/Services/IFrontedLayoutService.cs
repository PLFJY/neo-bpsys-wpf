using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台布局配置读写服务。
/// </summary>
public interface IFrontedLayoutService
{
    /// <summary>
    /// 加载以窗口为中心的 v3 布局配置。
    /// </summary>
    /// <param name="canonicalWindowId">窗口的 Canonical ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// 加载的配置。当激活包、内置资源均缺失或损坏时返回内存空模板，永不返回 <see langword="null"/>。
    /// 当 JSON 反序列化结果为 <c>null</c> 时视为损坏，抛出 <see cref="System.IO.InvalidDataException"/>。
    /// </returns>
    Task<FrontedWindowConfig> LoadWindowConfigAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载以窗口为中心的 v3 布局配置并返回来源元数据。
    /// </summary>
    /// <param name="canonicalWindowId">窗口的 Canonical ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载结果。</returns>
    Task<FrontedLayoutLoadResult> LoadWindowConfigWithMetadataAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存以窗口为中心的 v3 布局配置。
    /// </summary>
    /// <param name="canonicalWindowId">窗口的 Canonical ID。</param>
    /// <param name="config">要保存的配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveWindowConfigAsync(
        string canonicalWindowId,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default);
}
