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

    public FrontedLoopPolicy LoopPolicy { get; set; } = new();
}

