namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>
/// Manages Windows file association for <c>.bpui</c> layout package files.
/// </summary>
public interface IBpuiFileAssociationService
{
    /// <summary>
    /// Determines whether <c>.bpui</c> files are currently associated with this application.
    /// </summary>
    /// <returns><see langword="true"/> when the current effective association points to this application.</returns>
    bool IsAssociated();

    /// <summary>
    /// Ensures the current-user <c>.bpui</c> file association points to this application.
    /// </summary>
    void Associate();

    /// <summary>
    /// Removes the current-user <c>.bpui</c> file association if it points to this application.
    /// </summary>
    void RemoveAssociation();

    /// <summary>
    /// Silently checks and repairs the file association according to user settings.
    /// </summary>
    /// <param name="shouldAssociate">Whether the association should be enabled.</param>
    void EnsureAssociationState(bool shouldAssociate);
}
