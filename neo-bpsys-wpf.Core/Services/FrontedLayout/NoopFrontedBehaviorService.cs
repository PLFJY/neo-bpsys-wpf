using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// No-op behavior service used when behavior persistence is unavailable.
/// </summary>
public sealed class NoopFrontedBehaviorService : IFrontedBehaviorService
{
    /// <inheritdoc />
    public Task<FrontedBehaviorDocument> LoadDocumentAsync(
        string windowType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = windowType,
            CanvasName = FrontedLayoutConstants.BaseCanvasName
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
