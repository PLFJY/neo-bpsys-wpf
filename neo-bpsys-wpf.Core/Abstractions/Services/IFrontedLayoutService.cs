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
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载的配置，缺失时返回 <see langword="null"/>。</returns>
    Task<FrontedWindowConfig?> LoadWindowConfigAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载以窗口为中心的 v3 布局配置并返回来源元数据。
    /// </summary>
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载结果。</returns>
    Task<FrontedLayoutLoadResult> LoadWindowConfigWithMetadataAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存以窗口为中心的 v3 布局配置。
    /// </summary>
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <param name="config">要保存的配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveWindowConfigAsync(
        string windowTypeName,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default);
}
