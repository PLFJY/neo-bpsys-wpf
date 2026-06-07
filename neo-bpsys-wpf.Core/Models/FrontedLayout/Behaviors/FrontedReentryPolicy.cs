namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public enum FrontedReentryPolicy
{
    InterruptPrevious,
    IgnoreIfRunning,
    Queue,
    AllowParallel
}

