namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class TriggerDescriptor
{
    public string EventType { get; set; } = string.Empty;

    public string? Source { get; set; }

    public List<TriggerFilter> Filters { get; set; } = [];
}

