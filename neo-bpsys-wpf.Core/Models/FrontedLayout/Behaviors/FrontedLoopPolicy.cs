namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedLoopPolicy
{
    public int RepeatCount { get; set; } = -1;

    public bool AutoReverse { get; set; }

    public int IntervalMs { get; set; }

    public FrontedLoopStopMode StopMode { get; set; } = FrontedLoopStopMode.RunStopGraph;

    public bool ResetOnStop { get; set; } = true;

    public FrontedReentryPolicy ReentryPolicy { get; set; } = FrontedReentryPolicy.IgnoreIfRunning;
}

