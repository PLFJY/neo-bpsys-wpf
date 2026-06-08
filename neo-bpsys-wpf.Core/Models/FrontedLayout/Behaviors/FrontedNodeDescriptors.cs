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
    EventPath,
    PropertyName
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
    /// <summary>
    /// Gets the stable property name stored in node JSON.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the localization key for the property label.
    /// </summary>
    public required string DisplayNameKey { get; init; }

    /// <summary>
    /// Gets the high-level persisted value type.
    /// </summary>
    public FrontedNodePropertyType PropertyType { get; init; }

    /// <summary>
    /// Gets the default JSON value for newly created nodes.
    /// </summary>
    public JsonElement DefaultValue { get; init; }

    /// <summary>
    /// Gets the editor kind used by the Designer UI.
    /// </summary>
    public FrontedNodePropertyEditorKind EditorKind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the available editor options.
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>
    /// Gets an optional display unit for numeric values.
    /// </summary>
    public string? Unit { get; init; }
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
