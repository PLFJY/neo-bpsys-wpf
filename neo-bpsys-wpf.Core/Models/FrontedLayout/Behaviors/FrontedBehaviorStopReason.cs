namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Describes why an active loop behavior was requested to stop.
/// </summary>
public enum FrontedBehaviorStopReason
{
    /// <summary>
    /// The user manually cleared active loop animations.
    /// </summary>
    ManualClear,

    /// <summary>
    /// Game guidance was cancelled.
    /// </summary>
    GuidanceCancelled,

    /// <summary>
    /// Game guidance was stopped or completed.
    /// </summary>
    GuidanceStopped,

    /// <summary>
    /// The fronted window was hidden or closed.
    /// </summary>
    WindowHidden,

    /// <summary>
    /// The active layout package was switched.
    /// </summary>
    PackageSwitched,

    /// <summary>
    /// The fronted layout was reloaded.
    /// </summary>
    LayoutReloaded
}

