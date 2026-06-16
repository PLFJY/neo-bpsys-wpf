namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Host-facing SmartBP feature service.
/// </summary>
public interface ISmartBpFeatureService
{
    /// <summary>
    /// Raised when module load state changes.
    /// </summary>
    event EventHandler? ModuleStateChanged;

    /// <summary>
    /// Whether the SmartBP module is currently loaded.
    /// </summary>
    bool IsModuleLoaded { get; }

    /// <summary>
    /// Runs SmartBP post-game data autofill if the module is loaded.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Asynchronous task.</returns>
    Task AutoFillGameDataAsync(CancellationToken cancellationToken = default);
}
