using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedBehavior
{
    public Guid BehaviorId { get; set; } = FrontedBehaviorGuidHelper.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public FrontedBehaviorKind Kind { get; set; } = FrontedBehaviorKind.OneShot;

    public TriggerDescriptor? Trigger { get; set; }

    public FrontedNodeGraph Graph { get; set; } = new();

    public TriggerDescriptor? StartTrigger { get; set; }

    public FrontedNodeGraph StartGraph { get; set; } = new();

    public FrontedNodeGraph LoopGraph { get; set; } = new();

    /// <summary>
    /// Gets or sets the stop triggers for loop behaviors. Any matching trigger stops the loop.
    /// </summary>
    public List<TriggerDescriptor> StopTriggers { get; set; } = [];

    public FrontedNodeGraph StopGraph { get; set; } = new();

    /// <summary>
    /// Gets or sets the trigger descriptor used by transition behavior matching.
    /// </summary>
    public TriggerDescriptor? TransitionTrigger { get; set; }

    /// <summary>
    /// Gets or sets the graph that runs before the business state change is committed.
    /// </summary>
    public FrontedNodeGraph ExitGraph { get; set; } = new();

    /// <summary>
    /// Gets or sets the graph that runs after the business state change is committed.
    /// </summary>
    public FrontedNodeGraph EnterGraph { get; set; } = new();

    /// <summary>
    /// OneShot 行为的重入策略。Loop 行为的重入策略在 <see cref="LoopPolicy"/> 中配置。
    /// </summary>
    public FrontedReentryPolicy ReentryPolicy { get; set; } = FrontedReentryPolicy.InterruptPrevious;

    public FrontedLoopPolicy LoopPolicy { get; set; } = new();
}

