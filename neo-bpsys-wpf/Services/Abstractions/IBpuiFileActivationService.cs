namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>
/// Handles operating-system activation requests for <c>.bpui</c> layout package files.
/// </summary>
public interface IBpuiFileActivationService
{
    /// <summary>
    /// Starts listening for <c>.bpui</c> file paths forwarded by later application instances.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    void StartListening(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops listening for forwarded <c>.bpui</c> file paths.
    /// </summary>
    void StopListening();

    /// <summary>
    /// Forwards a <c>.bpui</c> path to the already running application instance.
    /// </summary>
    /// <param name="packagePath">Package file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the path was forwarded successfully.</returns>
    Task<bool> TryForwardToRunningInstanceAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports and activates a <c>.bpui</c> layout package.
    /// </summary>
    /// <param name="packagePath">Package file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import and activation result.</returns>
    Task<BpuiFileActivationResult> OpenPackageAsync(string packagePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result produced when opening a <c>.bpui</c> package from the operating system.
/// </summary>
/// <param name="Success">Whether the package was imported and activated.</param>
/// <param name="PackageId">Imported package id.</param>
/// <param name="ErrorMessage">Failure reason, if any.</param>
public sealed record BpuiFileActivationResult(bool Success, string? PackageId, string? ErrorMessage);
