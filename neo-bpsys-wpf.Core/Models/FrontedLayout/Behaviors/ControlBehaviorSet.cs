namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class ControlBehaviorSet
{
    public Guid BehaviorGuid { get; set; }

    public string? DisplayName { get; set; }

    public List<FrontedBehavior> Behaviors { get; set; } = [];
}

