namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedBehaviorEventDescriptor
{
    public string EventType { get; set; } = string.Empty;

    public string DisplayNameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public List<FrontedBehaviorEventPayloadField> PayloadFields { get; set; } = [];
}
