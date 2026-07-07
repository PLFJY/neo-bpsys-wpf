namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Prepares and cleans the isolated game tutorial sandbox.
/// </summary>
public interface IGameTutorialSandboxService
{
    /// <summary>Prepares sandbox state before a guided flow starts.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PrepareAsync(CancellationToken cancellationToken = default);

    /// <summary>Cleans sandbox state after completion, skip, or failure.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op sandbox implementation used until the host supplies game-specific behavior.
/// </summary>
public sealed class NoOpGameTutorialSandboxService : IGameTutorialSandboxService
{
    /// <inheritdoc />
    public Task PrepareAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task CleanupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
