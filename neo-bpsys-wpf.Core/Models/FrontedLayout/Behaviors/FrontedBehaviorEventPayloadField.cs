namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedBehaviorEventPayloadField
{
    public string Path { get; set; } = string.Empty;

    public string DisplayNameKey { get; set; } = string.Empty;

    public string TypeName { get; set; } = "string";

    public bool IsCommonFilterTarget { get; set; }
}
