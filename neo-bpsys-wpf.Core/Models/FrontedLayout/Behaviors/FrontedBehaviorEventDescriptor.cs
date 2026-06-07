namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedBehaviorEventDescriptor
{
    public string EventType { get; set; } = string.Empty;

    public string DisplayNameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string CategoryDisplayNameKey { get; set; } = string.Empty;

    public int Order { get; set; }

    public List<FrontedBehaviorEventPayloadField> PayloadFields { get; set; } = [];
}
