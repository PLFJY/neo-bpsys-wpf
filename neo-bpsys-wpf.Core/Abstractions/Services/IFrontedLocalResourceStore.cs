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
}
