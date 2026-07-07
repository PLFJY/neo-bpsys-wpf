namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Applies the language selected from a tutorial welcome overlay.
/// </summary>
public interface ITutorialLanguageService
{
    /// <summary>Applies and persists the selected language.</summary>
    /// <param name="cultureName">Culture name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyLanguageAsync(string cultureName, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op tutorial language service.
/// </summary>
public sealed class NoOpTutorialLanguageService : ITutorialLanguageService
{
    /// <inheritdoc />
    public Task ApplyLanguageAsync(string cultureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
