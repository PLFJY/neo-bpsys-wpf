using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Phase 1 behavior service placeholder. Real persistence and runtime cleanup come later.
/// </summary>
public sealed class NoopFrontedBehaviorService : IFrontedBehaviorService
{
    /// <inheritdoc />
    public Task<FrontedBehaviorDocument> LoadDocumentAsync(
        string windowType,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = windowType,
            CanvasName = canvasName
        });
    }

    /// <inheritdoc />
    public Task SaveDocumentAsync(
        FrontedBehaviorDocument document,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void RemoveBehaviors(Guid behaviorGuid)
    {
    }
}
