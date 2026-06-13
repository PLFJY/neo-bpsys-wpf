namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Snapshot of an active loop behavior tracked by the runtime registry.
/// </summary>
public sealed class ActiveLoopBehaviorState
{
    /// <summary>
    /// Gets or sets the behavior document identifier.
    /// </summary>
    public Guid BehaviorId { get; set; }

    /// <summary>
    /// Gets or sets the owning control behavior guid.
    /// </summary>
    public Guid BehaviorGuid { get; set; }

    /// <summary>
    /// Gets or sets the fronted window type.
    /// </summary>
    public string? WindowType { get; set; }

    /// <summary>
    /// Gets or sets the owning control display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the active behavior model.
    /// </summary>
    public FrontedBehavior Behavior { get; set; } = new();
}
