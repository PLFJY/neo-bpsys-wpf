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

    /// <summary>
    /// Copies a local font into a layout package resource store and returns font options for the copied file.
    /// </summary>
    /// <param name="sourcePath">Source font path.</param>
    /// <param name="packageId">Target package id.</param>
    /// <param name="packageRoot">Target package root.</param>
    /// <returns>Stored font results, one per discovered font family.</returns>
    IReadOnlyList<FrontedLocalFontResourceStoreResult> StorePackageFontWithResult(
        string sourcePath,
        string packageId,
        string packageRoot);
}

/// <summary>
/// Result of storing an editor-local resource.
/// </summary>
public sealed record FrontedLocalResourceStoreResult
{
    /// <summary>
    /// Initializes a local resource store result.
    /// </summary>
    /// <param name="resourceUri">Stored bpui resource URI.</param>
    /// <param name="physicalPath">Physical copied file path.</param>
    /// <param name="wasNewlyCreated">Whether the file was newly copied.</param>
    public FrontedLocalResourceStoreResult(string resourceUri, string physicalPath, bool wasNewlyCreated)
    {
        ResourceUri = resourceUri;
        PhysicalPath = physicalPath;
        WasNewlyCreated = wasNewlyCreated;
    }

    /// <summary>
    /// Stored bpui resource URI.
    /// </summary>
    public string ResourceUri { get; }

    /// <summary>
    /// Physical copied file path.
    /// </summary>
    public string PhysicalPath { get; }

    /// <summary>
    /// Whether the file was newly copied.
    /// </summary>
    public bool WasNewlyCreated { get; }
}

/// <summary>
/// Result of storing a package font resource.
/// </summary>
public sealed record FrontedLocalFontResourceStoreResult
{
    /// <summary>
    /// Initializes a package font store result.
    /// </summary>
    /// <param name="resourceUri">Stored bpui font URI including the family fragment.</param>
    /// <param name="physicalPath">Physical copied font path.</param>
    /// <param name="wasNewlyCreated">Whether the file was newly copied.</param>
    /// <param name="fontFamilyName">Discovered font family name.</param>
    public FrontedLocalFontResourceStoreResult(
        string resourceUri,
        string physicalPath,
        bool wasNewlyCreated,
        string fontFamilyName)
    {
        ResourceUri = resourceUri;
        PhysicalPath = physicalPath;
        WasNewlyCreated = wasNewlyCreated;
        FontFamilyName = fontFamilyName;
    }

    /// <summary>
    /// Stored bpui font URI including the family fragment.
    /// </summary>
    public string ResourceUri { get; }

    /// <summary>
    /// Physical copied font path.
    /// </summary>
    public string PhysicalPath { get; }

    /// <summary>
    /// Whether the file was newly copied.
    /// </summary>
    public bool WasNewlyCreated { get; }

    /// <summary>
    /// Discovered font family name.
    /// </summary>
    public string FontFamilyName { get; }
}
