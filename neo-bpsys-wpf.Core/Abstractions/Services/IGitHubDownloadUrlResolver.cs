namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>通过应用配置的镜像链解析 GitHub 资产 URL。</summary>
public interface IGitHubDownloadUrlResolver
{
    /// <summary>解析 URL，当无适用镜像时返回原始 URL。</summary>
    /// <param name="url">原始 GitHub URL。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>用于下载的 URL。</returns>
    Task<string> ResolveAsync(string url, CancellationToken cancellationToken = default);
    /// <summary>清除缓存的镜像探测结果。</summary>
    void ResetCache();
}
