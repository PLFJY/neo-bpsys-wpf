using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Phase 1 behavior service placeholder. Real persistence and runtime cleanup come later.
/// </summary>
public sealed class NoopFrontedBehaviorService : IFrontedBehaviorService
{
    /// <inheritdoc />
    public void RemoveBehaviors(Guid behaviorGuid)
    {
    }
}

