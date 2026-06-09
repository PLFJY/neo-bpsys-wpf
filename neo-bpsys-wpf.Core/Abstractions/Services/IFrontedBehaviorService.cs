namespace neo_bpsys_wpf.Core.Abstractions.Services;

using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Handles fronted behavior data owned by a fronted window identity.
/// </summary>
public interface IFrontedBehaviorService
{
    /// <summary>
    /// Loads the behavior document for the specified fronted window.
    /// </summary>
    /// <param name="windowType">The full window type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The behavior document.</returns>
    Task<FrontedBehaviorDocument> LoadDocumentAsync(
        string windowType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a behavior document for its <see cref="FrontedBehaviorDocument.WindowType" />.
    /// </summary>
    /// <param name="document">The behavior document to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveDocumentAsync(
        FrontedBehaviorDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes behavior data attached directly to the specified behavior target.
    /// </summary>
    /// <param name="behaviorGuid">The behavior target GUID.</param>
    void RemoveBehaviors(Guid behaviorGuid);
}
