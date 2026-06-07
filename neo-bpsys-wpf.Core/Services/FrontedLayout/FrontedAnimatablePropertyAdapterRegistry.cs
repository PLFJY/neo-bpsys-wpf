using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedAnimatablePropertyAdapterRegistry(
    IEnumerable<IAnimatablePropertyAdapter> adapters) : IAnimatablePropertyAdapterRegistry
{
    private readonly IReadOnlyList<IAnimatablePropertyAdapter> _adapters = adapters.ToArray();

    public FrontedAnimatablePropertyAdapterRegistry()
        : this(
        [
            new BackgroundTintAnimatablePropertyAdapter(),
            new ShapeAnimatablePropertyAdapter(),
            new TextAnimatablePropertyAdapter(),
            new FrameworkElementCommonAdapter()
        ])
    {
    }

    public IAnimatablePropertyAdapter? Resolve(FrontedAnimationTarget target, string propertyName) =>
        _adapters.FirstOrDefault(adapter => adapter.CanHandle(target, propertyName));
}
