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
        @"https://hk.gh-proxy.com/",
        @"https://cdn.gh-proxy.com/",
        @"https://edgeone.gh-proxy.com/",
        @"https://gh.plfjy.top/",
        @""
    ];
}
