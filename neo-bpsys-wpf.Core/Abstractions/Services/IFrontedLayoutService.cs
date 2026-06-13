using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台布局配置读写服务。
/// </summary>
public interface IFrontedLayoutService
{
    /// <summary>
    /// Loads a window-centric v3 layout config.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded config, or <see langword="null"/> when missing.</returns>
    Task<FrontedWindowConfig?> LoadWindowConfigAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a window-centric v3 layout config and returns source metadata.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The load result.</returns>
    Task<FrontedLayoutLoadResult> LoadWindowConfigWithMetadataAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a window-centric v3 layout config.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="config">The config to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveWindowConfigAsync(
        string windowTypeName,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default);
}
