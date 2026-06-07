using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedNodePortKind
{
    FlowIn,
    FlowOut,
    ValueIn,
    ValueOut
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedNodePropertyType
{
    String,
    Number,
    Boolean,
    Color,
    Enum
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedNodePropertyEditorKind
{
    Text,
    Number,
    Boolean,
    Enum,
    Color,
    ControlReference,
    EventPath
}

public sealed class FrontedNodePortDescriptor
{
    public required string Name { get; init; }
    public required string DisplayNameKey { get; init; }
    public FrontedNodePortKind PortKind { get; init; }
    public string? ValueType { get; init; }
    public bool IsRequired { get; init; }
}

public sealed class FrontedNodePropertyDescriptor
{
    public required string Name { get; init; }
    public required string DisplayNameKey { get; init; }
    public FrontedNodePropertyType PropertyType { get; init; }
    public JsonElement DefaultValue { get; init; }
    public FrontedNodePropertyEditorKind EditorKind { get; init; }
    public bool IsRequired { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
}

public sealed class FrontedNodeTypeDescriptor
{
    public required string NodeType { get; init; }
    public required string DisplayNameKey { get; init; }
    public required string DescriptionKey { get; init; }
    public required string Category { get; init; }
    public IReadOnlyList<FrontedNodePortDescriptor> InputPorts { get; init; } = [];
    public IReadOnlyList<FrontedNodePortDescriptor> OutputPorts { get; init; } = [];
    public IReadOnlyList<FrontedNodePropertyDescriptor> Properties { get; init; } = [];
    public string? Icon { get; init; }
}
