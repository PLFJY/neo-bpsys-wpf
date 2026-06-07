namespace neo_bpsys_wpf.Core.Abstractions.Services;

using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Handles fronted behavior data owned by a fronted control identity.
/// </summary>
public interface IFrontedBehaviorService
{
    /// <summary>
    /// Loads the behavior document for the specified fronted canvas.
    /// </summary>
    Task<FrontedBehaviorDocument> LoadDocumentAsync(
        string windowType,
        string canvasName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a behavior document for its <see cref="FrontedBehaviorDocument.WindowType" /> and
    /// <see cref="FrontedBehaviorDocument.CanvasName" />.
    /// </summary>
    Task SaveDocumentAsync(
        FrontedBehaviorDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes behavior data attached directly to the specified behavior target.
    /// </summary>
    void RemoveBehaviors(Guid behaviorGuid);
}
