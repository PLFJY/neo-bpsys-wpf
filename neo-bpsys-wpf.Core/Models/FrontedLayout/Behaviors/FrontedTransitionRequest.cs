namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Describes a fronted transition request whose graphs should wrap a business state change.
/// </summary>
public sealed class FrontedTransitionRequest
{
    /// <summary>
    /// Gets or sets the fronted window type that should receive the transition.
    /// </summary>
    public string WindowType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transition event type used by <see cref="FrontedBehavior.TransitionTrigger"/>.
    /// </summary>
    public string TransitionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the behavior GUID of the target control.
    /// </summary>
    public Guid TargetBehaviorGuid { get; set; }

    /// <summary>
    /// Gets or sets the display name of the target control, used for diagnostics and legacy fallback matching.
    /// </summary>
    public string TargetDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets stable machine-readable transition payload values.
    /// </summary>
    public Dictionary<string, object?> Payload { get; set; } = [];
}
