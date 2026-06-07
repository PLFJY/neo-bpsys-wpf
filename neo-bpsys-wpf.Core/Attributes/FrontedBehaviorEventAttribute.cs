using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Attributes;

[AttributeUsage(AttributeTargets.Event)]
public sealed class FrontedBehaviorEventAttribute(string eventType) : Attribute
{
    public string EventType { get; } = eventType;

    public string DisplayNameKey { get; init; } = string.Empty;

    public string DescriptionKey { get; init; } = string.Empty;

    public string CategoryKey { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public int Order { get; init; }

    public bool IsEnabled { get; init; } = true;
}

[AttributeUsage(AttributeTargets.Event, AllowMultiple = true)]
public sealed class FrontedBehaviorEventPayloadAttribute(string path) : Attribute
{
    public string Path { get; } = path;

    public string DisplayNameKey { get; init; } = string.Empty;

    public string DescriptionKey { get; init; } = string.Empty;

    public Type? ValueType { get; init; }

    public string? TypeName { get; init; }

    public FrontedBehaviorPayloadSource Source { get; init; }

    public string? SourcePath { get; init; }

    public bool IsCommonFilterTarget { get; init; } = true;
}
