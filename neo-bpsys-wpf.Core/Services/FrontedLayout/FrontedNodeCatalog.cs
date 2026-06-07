using System.Text.Json;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedNodeCatalog
{
    private static readonly IReadOnlyList<FrontedNodeTypeDescriptor> BuiltInNodes = CreateNodes();
    private static readonly IReadOnlyDictionary<string, FrontedNodeTypeDescriptor> BuiltInNodesByType =
        BuiltInNodes.ToDictionary(node => node.NodeType, StringComparer.Ordinal);

    public IReadOnlyList<FrontedNodeTypeDescriptor> Nodes => BuiltInNodes;

    public FrontedNodeTypeDescriptor? Find(string nodeType) =>
        BuiltInNodesByType.GetValueOrDefault(nodeType);

    public FrontedNode CreateNode(string nodeType, double x = 40, double y = 40)
    {
        var descriptor = Find(nodeType) ?? throw new ArgumentException($"Unknown node type '{nodeType}'.", nameof(nodeType));
        return new FrontedNode
        {
            NodeType = descriptor.NodeType,
            DisplayName = descriptor.DisplayNameKey,
            X = x,
            Y = y,
            Properties = descriptor.Properties.ToDictionary(
                property => property.Name,
                property => property.DefaultValue.Clone(),
                StringComparer.Ordinal)
        };
    }

    private static IReadOnlyList<FrontedNodeTypeDescriptor> CreateNodes()
    {
        var flowIn = Port("In", FrontedNodePortKind.FlowIn);
        var flowOut = Port("Out", FrontedNodePortKind.FlowOut);
        var valueOut = Port("Value", FrontedNodePortKind.ValueOut, "object");
        return
        [
            Node("flow.start", "Flow", [], [flowOut]),
            Node("flow.end", "Flow", [flowIn], []),
            Node("flow.delay", "Flow", [flowIn], [flowOut], Prop("DurationMs", FrontedNodePropertyType.Number, 300, FrontedNodePropertyEditorKind.Number, true)),
            Node("flow.sequence", "Flow", [flowIn], [Port("Step1", FrontedNodePortKind.FlowOut), Port("Step2", FrontedNodePortKind.FlowOut), Port("Step3", FrontedNodePortKind.FlowOut)]),
            Node("flow.parallel", "Flow", [flowIn], [Port("Branch1", FrontedNodePortKind.FlowOut), Port("Branch2", FrontedNodePortKind.FlowOut), Port("Branch3", FrontedNodePortKind.FlowOut)]),
            Node("flow.if", "Flow", [flowIn], [Port("True", FrontedNodePortKind.FlowOut), Port("False", FrontedNodePortKind.FlowOut)],
                Prop("Left", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.EventPath, true),
                Prop("Operator", FrontedNodePropertyType.Enum, TriggerFilterOperator.Equals.ToString(), FrontedNodePropertyEditorKind.Enum, true, Enum.GetNames<TriggerFilterOperator>()),
                Prop("Right", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text)),
            Node("action.log", "Action", [flowIn], [flowOut], Prop("Message", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text)),
            Node("action.setProperty", "Action", [flowIn], [flowOut],
                Prop("Target", FrontedNodePropertyType.String, "Self", FrontedNodePropertyEditorKind.ControlReference, true),
                Prop("PropertyName", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text, true),
                Prop("Value", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text)),
            Node("action.resetProperty", "Action", [flowIn], [flowOut],
                Prop("Target", FrontedNodePropertyType.String, "Self", FrontedNodePropertyEditorKind.ControlReference, true),
                Prop("PropertyName", FrontedNodePropertyType.String, "All", FrontedNodePropertyEditorKind.Text, true)),
            Node("action.animateProperty", "Action", [flowIn], [flowOut],
                Prop("Target", FrontedNodePropertyType.String, "Self", FrontedNodePropertyEditorKind.ControlReference, true),
                Prop("PropertyName", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text, true),
                Prop("From", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text),
                Prop("To", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text),
                Prop("DurationMs", FrontedNodePropertyType.Number, 300, FrontedNodePropertyEditorKind.Number, true),
                Prop("Easing", FrontedNodePropertyType.String, "Linear", FrontedNodePropertyEditorKind.Text)),
            Node("value.number", "Value", [], [valueOut], Prop("Value", FrontedNodePropertyType.Number, 0, FrontedNodePropertyEditorKind.Number)),
            Node("value.string", "Value", [], [valueOut], Prop("Value", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text)),
            Node("value.boolean", "Value", [], [valueOut], Prop("Value", FrontedNodePropertyType.Boolean, false, FrontedNodePropertyEditorKind.Boolean)),
            Node("value.color", "Value", [], [Port("Value", FrontedNodePortKind.ValueOut, "color")], Prop("Value", FrontedNodePropertyType.Color, "#FFFFFFFF", FrontedNodePropertyEditorKind.Color)),
            Node("value.eventValue", "Value", [], [valueOut], Prop("Path", FrontedNodePropertyType.String, "Event.", FrontedNodePropertyEditorKind.EventPath, true)),
            Node("value.selfTag", "Value", [], [valueOut], Prop("Path", FrontedNodePropertyType.String, "SelfTag.", FrontedNodePropertyEditorKind.Text, true)),
            Node("value.controlReference", "Value", [], [Port("Value", FrontedNodePortKind.ValueOut, "control")], Prop("Value", FrontedNodePropertyType.String, "Self", FrontedNodePropertyEditorKind.ControlReference, true))
        ];
    }

    private static FrontedNodeTypeDescriptor Node(
        string nodeType,
        string category,
        IReadOnlyList<FrontedNodePortDescriptor> inputs,
        IReadOnlyList<FrontedNodePortDescriptor> outputs,
        params FrontedNodePropertyDescriptor[] properties) =>
        new()
        {
            NodeType = nodeType,
            DisplayNameKey = $"Designer.Graph.Node.{NodeKey(nodeType)}",
            DescriptionKey = $"Designer.Graph.Node.{NodeKey(nodeType)}.Description",
            Category = category,
            InputPorts = inputs,
            OutputPorts = outputs,
            Properties = properties
        };

    private static FrontedNodePortDescriptor Port(string name, FrontedNodePortKind kind, string? valueType = null) =>
        new()
        {
            Name = name,
            DisplayNameKey = $"Designer.Graph.Port.{name}",
            PortKind = kind,
            ValueType = valueType
        };

    private static FrontedNodePropertyDescriptor Prop(
        string name,
        FrontedNodePropertyType type,
        object value,
        FrontedNodePropertyEditorKind editor,
        bool required = false,
        IReadOnlyList<string>? options = null) =>
        new()
        {
            Name = name,
            DisplayNameKey = $"Designer.Graph.Property.{name}",
            PropertyType = type,
            DefaultValue = JsonSerializer.SerializeToElement(value),
            EditorKind = editor,
            IsRequired = required,
            Options = options ?? []
        };

    private static string NodeKey(string nodeType) =>
        string.Concat(nodeType.Split('.').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
