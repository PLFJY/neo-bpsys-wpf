using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IAnimatablePropertyAdapterRegistry
{
    IAnimatablePropertyAdapter? Resolve(FrontedAnimationTarget target, string propertyName);
}
