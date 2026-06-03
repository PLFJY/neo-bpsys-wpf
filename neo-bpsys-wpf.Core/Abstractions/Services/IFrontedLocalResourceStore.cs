namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Stores editor-local resources for Designer v3 layouts.
/// </summary>
public interface IFrontedLocalResourceStore
{
    /// <summary>
    /// Copies a local image into the editor-local bpui resource store and returns a bpui URI.
    /// </summary>
    string StoreImage(string sourcePath);

    /// <summary>
    /// Copies a local image and returns details useful for editor session cleanup.
    /// </summary>
    FrontedLocalResourceStoreResult StoreImageWithResult(string sourcePath);

    /// <summary>
    /// Resolves a local bpui resource URI to its physical file path.
    /// </summary>
    bool TryGetPhysicalPath(string resourceUri, out string physicalPath);
}

public sealed record FrontedLocalResourceStoreResult(
    string ResourceUri,
    string PhysicalPath,
    bool WasNewlyCreated);
