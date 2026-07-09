namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Defines shared delays used after tutorial-driven UI transitions.
/// </summary>
public static class TutorialTransitionDelays
{
    /// <summary>Delay applied after page navigation has reported completion.</summary>
    public static readonly TimeSpan NavigationSettleDelay = TimeSpan.FromMilliseconds(450);

    /// <summary>Delay applied after switching the active tutorial window.</summary>
    public static readonly TimeSpan WindowSwitchSettleDelay = TimeSpan.FromMilliseconds(450);

    /// <summary>Delay applied after a tutorial scroll operation.</summary>
    public static readonly TimeSpan ScrollSettleDelay = TimeSpan.FromMilliseconds(350);
}
