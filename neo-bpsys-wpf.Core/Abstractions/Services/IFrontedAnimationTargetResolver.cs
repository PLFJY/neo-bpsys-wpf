using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IFrontedAnimationTargetResolver
{
    FrontedAnimationTarget? Resolve(
        FrontedAnimationTargetReference reference,
        FrontedAnimationExecutionContext context);
}
