using neo_bpsys_wpf.Models.Plugins;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 提供应用更新和插件市场共用的下载镜像预设。
/// </summary>
public static class DownloadMirrorPresets
{
    /// <summary>
    /// 默认使用的镜像地址。
    /// </summary>
    public const string DefaultMirror = "https://ghproxy.net/";

    /// <summary>
    /// 可供选择的 GhProxy 镜像列表，空字符串表示直连。
    /// </summary>
    public static readonly IReadOnlyList<string> GhProxyMirrorList =
    [
        @"https://gh-proxy.com/",
        @"https://ghproxy.net/",
        @"https://ghfast.top/",
        @"https://cdn.gh-proxy.com/",
        @"https://edgeone.gh-proxy.com/",
        @"https://gh.plfjy.top/",
        @"https://gh.jasonzeng.dev/",
        @"https://gh-proxy.org/",
        @"https://v4.gh-proxy.org/",
        @"https://v6.gh-proxy.org/",
        @"https://cdn.gh-proxy.org/",
        @""
    ];

    /// <summary>
    /// 从已完成测速的镜像选项中找出延迟最低的可用项。
    /// </summary>
    /// <param name="options">要比较的镜像选项。</param>
    /// <returns>延迟最低的可用镜像；所有测速均失败或尚未完成时返回 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <see langword="null"/>。</exception>
    public static PluginMarketMirrorOption? FindLowestLatencyOption(
        IEnumerable<PluginMarketMirrorOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .Where(option => option.LatencyMs >= 0)
            .MinBy(option => option.LatencyMs);
    }
}
