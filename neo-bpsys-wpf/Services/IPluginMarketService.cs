using neo_bpsys_wpf.Models.Plugins;

namespace neo_bpsys_wpf.Services;

public interface IPluginMarketService
{
    bool IsDownloading { get; }

    double DownloadProgress { get; }

    double DownloadBytesPerSecond { get; }

    string CurrentDownloadPluginId { get; }

    event EventHandler? DownloadStateChanged;

    Task<IReadOnlyList<PluginMarketItem>> GetMarketPluginsAsync(CancellationToken cancellationToken = default);

    Task<string> GetReadmeMarkdownAsync(PluginMarketItem item, CancellationToken cancellationToken = default);

    Task<PluginPackageDownloadResult> DownloadPluginPackageAsync(PluginMarketItem item,
        CancellationToken cancellationToken = default);

    void CancelDownload();

    void ResetMirrorCache();
}
