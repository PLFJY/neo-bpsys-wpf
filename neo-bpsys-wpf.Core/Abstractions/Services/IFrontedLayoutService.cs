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

    /// <summary>
    /// Deletes the user window layout.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteUserWindowLayoutAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a user window layout exists.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <returns><see langword="true"/> when the user layout exists.</returns>
    bool UserWindowLayoutExists(string windowTypeName);

    /// <summary>
    /// Gets the user window layout path.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <returns>The layout JSON path.</returns>
    string GetUserWindowLayoutPath(string windowTypeName);

    /// <summary>
    /// 获取用户布局根目录。
    /// </summary>
    string GetUserLayoutRootFolder();

    /// <summary>
    /// Loads the built-in default window layout without the fallback chain.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The built-in config, or <see langword="null"/> when missing or invalid.</returns>
    Task<FrontedWindowConfig?> LoadBuiltInDefaultWindowLayoutAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the built-in default window layout path.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <returns>The built-in layout JSON path.</returns>
    string GetBuiltInDefaultWindowLayoutPath(string windowTypeName);

    /// <summary>
    /// Gets the plugin default window layout path.
    /// </summary>
    /// <param name="pluginFolder">The plugin folder.</param>
    /// <param name="windowTypeName">The plugin-local window type name.</param>
    /// <returns>The plugin default layout JSON path.</returns>
    string GetPluginDefaultWindowLayoutPath(string pluginFolder, string windowTypeName);
}
