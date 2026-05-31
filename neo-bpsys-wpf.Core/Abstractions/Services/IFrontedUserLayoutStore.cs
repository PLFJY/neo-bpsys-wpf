using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Stores Designer v3 user layout files under the application data directory.
/// </summary>
public interface IFrontedUserLayoutStore
{
    bool Exists(string windowTypeName, string canvasName);

    Task<FrontedCanvasConfig?> LoadAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default);

    string GetLayoutPath(string windowTypeName, string canvasName);

    string GetLayoutFolder(string windowTypeName, string canvasName);

    string GetRootFolder();
}
