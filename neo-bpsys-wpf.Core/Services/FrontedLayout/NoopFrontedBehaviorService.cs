using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 行为持久化不可用时使用的空操作行为服务。
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
