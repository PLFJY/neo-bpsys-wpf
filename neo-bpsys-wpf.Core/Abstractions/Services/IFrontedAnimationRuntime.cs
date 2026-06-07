using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IFrontedAnimationRuntime
{
    Task ExecuteAsync(
        IReadOnlyList<FrontedGraphActionRequest> actions,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        FrontedGraphActionRequest action,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default);

    void ResetTarget(Guid behaviorGuid, FrontedAnimationExecutionContext context);

    void ResetAll(FrontedAnimationExecutionContext context);
}
