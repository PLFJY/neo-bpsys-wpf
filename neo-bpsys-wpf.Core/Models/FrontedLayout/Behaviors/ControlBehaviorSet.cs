using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Stores behaviors and generated animation parts for one fronted control.
/// </summary>
public sealed class ControlBehaviorSet
{
    /// <summary>
    /// Gets or sets the stable behavior identifier of the owning fronted control.
    /// </summary>
    public Guid BehaviorGuid { get; set; }

    /// <summary>
    /// Gets or sets the user-facing name of the owning fronted control.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets generated animation helper visuals owned by this behavior set.
    /// </summary>
    public List<FrontedAnimationPartConfig> AnimationParts { get; set; } = [];

    /// <summary>
    /// Gets or sets behavior graphs owned by this fronted control.
    /// </summary>
    public List<FrontedBehavior> Behaviors { get; set; } = [];
}

