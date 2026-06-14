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
        var propertyOptions = FrontedBehaviorPropertyMetadata.CommonPropertyNames;
        var targetLayerOptions = Enum.GetNames<FrontedAnimationTargetLayer>();
        var easingOptions = new[] { "Linear", "SineInOut", "CubicOut", "CubicIn", "CubicInOut", "BackOut" };
        return
        [
            Node("flow.start", "Flow", [], [flowOut]),
            Node("flow.end", "Flow", [flowIn], []),
            Node("flow.delay", "Flow", [flowIn], [flowOut], Prop("DurationMs", FrontedNodePropertyType.Number, 300, FrontedNodePropertyEditorKind.Number, true, unit: "ms")),
            Node("flow.parallel", "Flow", [flowIn],
                [.. FrontedParallelNodePorts.GetBranchPortNames(FrontedParallelNodePorts.MaxBranchCount).Select(name => Port(name, FrontedNodePortKind.FlowOut)), flowOut],
                Prop("BranchCount", FrontedNodePropertyType.Number, FrontedParallelNodePorts.DefaultBranchCount, FrontedNodePropertyEditorKind.Number)),
            Node("flow.if", "Flow", [flowIn], [Port("True", FrontedNodePortKind.FlowOut), Port("False", FrontedNodePortKind.FlowOut)],
                Prop("Left", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.EventPath, true),
                Prop("Operator", FrontedNodePropertyType.Enum, TriggerFilterOperator.Equals.ToString(), FrontedNodePropertyEditorKind.Enum, true, Enum.GetNames<TriggerFilterOperator>()),
                Prop("Right", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text)),
            Node("action.log", "Action", [flowIn], [flowOut], Prop("Message", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text)),
            Node("action.setProperty", "Action", [flowIn], [flowOut],
                Prop("Target", FrontedNodePropertyType.String, "Self", FrontedNodePropertyEditorKind.ControlReference, true),
                Prop("TargetLayer", FrontedNodePropertyType.Enum, FrontedAnimationTargetLayer.Auto.ToString(), FrontedNodePropertyEditorKind.Enum, true, targetLayerOptions),
                Prop("PropertyName", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.PropertyName, true, propertyOptions),
                Prop("Value", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text)),
            Node("action.resetProperty", "Action", [flowIn], [flowOut],
                Prop("Target", FrontedNodePropertyType.String, "Self", FrontedNodePropertyEditorKind.ControlReference, true),
                Prop("TargetLayer", FrontedNodePropertyType.Enum, FrontedAnimationTargetLayer.Auto.ToString(), FrontedNodePropertyEditorKind.Enum, true, targetLayerOptions),
                Prop("PropertyName", FrontedNodePropertyType.String, "All", FrontedNodePropertyEditorKind.PropertyName, true, ["All", .. propertyOptions])),
            Node("action.animateProperty", "Action", [flowIn], [flowOut],
                Prop("Target", FrontedNodePropertyType.String, "Self", FrontedNodePropertyEditorKind.ControlReference, true),
                Prop("TargetLayer", FrontedNodePropertyType.Enum, FrontedAnimationTargetLayer.Auto.ToString(), FrontedNodePropertyEditorKind.Enum, true, targetLayerOptions),
                Prop("PropertyName", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.PropertyName, true, propertyOptions),
                Prop("From", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text),
                Prop("To", FrontedNodePropertyType.String, "", FrontedNodePropertyEditorKind.Text),
                Prop("DurationMs", FrontedNodePropertyType.Number, 300, FrontedNodePropertyEditorKind.Number, true, unit: "ms"),
                Prop("Easing", FrontedNodePropertyType.String, "Linear", FrontedNodePropertyEditorKind.Text, false, easingOptions),
                Prop("WaitForCompletion", FrontedNodePropertyType.Boolean, true, FrontedNodePropertyEditorKind.Boolean)),
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
        IReadOnlyList<string>? options = null,
        string? unit = null) =>
        new()
        {
            Name = name,
            DisplayNameKey = $"Designer.Graph.Property.{name}",
            PropertyType = type,
            DefaultValue = JsonSerializer.SerializeToElement(value),
            EditorKind = editor,
            IsRequired = required,
            Options = options ?? [],
            Unit = unit
        };

    private static string NodeKey(string nodeType) =>
        string.Concat(nodeType.Split('.').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
