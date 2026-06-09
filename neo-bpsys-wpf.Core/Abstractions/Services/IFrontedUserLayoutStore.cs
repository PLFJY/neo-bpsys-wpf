using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Stores Designer v3 user layout files under the application data directory.
/// </summary>
public interface IFrontedUserLayoutStore
{
    /// <summary>
    /// Returns whether a user window layout exists.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <returns><see langword="true"/> when the user layout exists.</returns>
    bool Exists(string windowTypeName);

    /// <summary>
    /// Loads a user window layout.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded config, or <see langword="null"/> when no file exists.</returns>
    Task<FrontedWindowConfig?> LoadAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a user window layout.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="config">The config to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(
        string windowTypeName,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user window layout.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user window layout path.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <returns>The layout JSON path.</returns>
    string GetLayoutPath(string windowTypeName);

    /// <summary>
    /// Gets the user layout root folder.
    /// </summary>
    /// <returns>The user layout root folder.</returns>
    string GetRootFolder();

    /// <summary>
    /// Returns whether a legacy user canvas layout exists.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="canvasName">The canvas name.</param>
    /// <returns><see langword="true"/> when the legacy layout exists.</returns>
    bool LegacyCanvasExists(string windowTypeName, string canvasName);

    /// <summary>
    /// Loads a legacy user canvas layout for conversion helpers.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="canvasName">The canvas name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded legacy canvas config, or <see langword="null"/> when missing.</returns>
    Task<FrontedCanvasConfig?> LoadLegacyCanvasAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the legacy user canvas layout path.
    /// </summary>
    /// <param name="windowTypeName">The full window type name.</param>
    /// <param name="canvasName">The canvas name.</param>
    /// <returns>The legacy canvas layout path.</returns>
    string GetLegacyCanvasLayoutPath(string windowTypeName, string canvasName);
}
