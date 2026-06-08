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

    public TriggerDescriptor? EndTrigger { get; set; }

    public FrontedNodeGraph StopGraph { get; set; } = new();

    /// <summary>
    /// OneShot 行为的重入策略。Loop 行为的重入策略在 <see cref="LoopPolicy"/> 中配置。
    /// </summary>
    public FrontedReentryPolicy ReentryPolicy { get; set; } = FrontedReentryPolicy.InterruptPrevious;

    public FrontedLoopPolicy LoopPolicy { get; set; } = new();
}

