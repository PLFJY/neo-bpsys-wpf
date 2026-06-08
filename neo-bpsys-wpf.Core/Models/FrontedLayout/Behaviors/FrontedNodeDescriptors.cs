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

    /// <summary>
    /// 判断两个端口是否可以建立连接。
    /// </summary>
    public static bool AreCompatible(FrontedNodePortDescriptor source, FrontedNodePortDescriptor target)
    {
        // FlowOut → FlowIn
        if (source.PortKind == FrontedNodePortKind.FlowOut && target.PortKind == FrontedNodePortKind.FlowIn)
            return true;

        // ValueOut → ValueIn
        if (source.PortKind != FrontedNodePortKind.ValueOut || target.PortKind != FrontedNodePortKind.ValueIn)
            return false;

        // "object"（Any）类型兼容所有类型
        if (source.ValueType == FrontedNodePortValueType.Object || target.ValueType == FrontedNodePortValueType.Object)
            return true;

        // 相同具体类型 → 兼容
        if (string.Equals(source.ValueType, target.ValueType, StringComparison.Ordinal))
            return true;

        return false;
    }
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

/// <summary>
/// 定义端口值类型的字符串常量，用于 <see cref="FrontedNodePortDescriptor.ValueType"/>。
/// 避免 magic string 散落在各处。
/// </summary>
public static class FrontedNodePortValueType
{
    /// <summary>数字类型</summary>
    public const string Number = "number";
    /// <summary>字符串类型</summary>
    public const string String = "string";
    /// <summary>布尔类型</summary>
    public const string Boolean = "boolean";
    /// <summary>颜色类型</summary>
    public const string Color = "color";
    /// <summary>控件引用类型</summary>
    public const string Control = "control";
    /// <summary>任意/动态类型（如 eventValue、selfTag）</summary>
    public const string Object = "object";
}
