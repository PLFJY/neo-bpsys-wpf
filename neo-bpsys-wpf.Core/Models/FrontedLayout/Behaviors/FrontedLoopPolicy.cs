namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Loop 行为的循环策略配置。
/// </summary>
public sealed class FrontedLoopPolicy
{
    /// <summary>
    /// 重复次数（-1 表示无限循环）。
    /// </summary>
    public int RepeatCount { get; set; } = -1;

    /// <summary>
    /// 是否自动反向播放。
    /// </summary>
    public bool AutoReverse { get; set; }

    /// <summary>
    /// 循环间隔（毫秒）。
    /// </summary>
    public int IntervalMs { get; set; }

    /// <summary>
    /// 停止模式。
    /// </summary>
    public FrontedLoopStopMode StopMode { get; set; } = FrontedLoopStopMode.RunStopGraph;

    /// <summary>
    /// 停止时是否重置状态。
    /// </summary>
    public bool ResetOnStop { get; set; } = true;

    /// <summary>
    /// 重入策略。
    /// </summary>
    public FrontedReentryPolicy ReentryPolicy { get; set; } = FrontedReentryPolicy.IgnoreIfRunning;
}

