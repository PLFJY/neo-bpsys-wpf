namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedBehaviorEventPayloadField
{
    public string Path { get; set; } = string.Empty;

    public string DisplayNameKey { get; set; } = string.Empty;

    public string DescriptionKey { get; set; } = string.Empty;

    public string TypeName { get; set; } = "string";

    public FrontedBehaviorPayloadSource Source { get; set; }

    public string? SourcePath { get; set; }

    public bool IsCommonFilterTarget { get; set; }

    /// <summary>
    /// Gets or sets the stable enum names accepted by this payload field.
    /// </summary>
    public List<string> EnumValues { get; set; } = [];
}
