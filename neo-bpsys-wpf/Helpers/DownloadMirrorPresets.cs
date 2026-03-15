namespace neo_bpsys_wpf.Helpers;

public static class DownloadMirrorPresets
{
    public const string DefaultMirror = "https://ghproxy.net/";

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
