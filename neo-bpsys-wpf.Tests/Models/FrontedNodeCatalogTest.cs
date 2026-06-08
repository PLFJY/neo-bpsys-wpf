using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedNodeCatalogTest
{
    [Fact]
    public void NodeCatalog_ContainsRequiredPhase3Nodes()
    {
        var catalog = new FrontedNodeCatalog();
        var nodeTypes = catalog.Nodes.Select(node => node.NodeType).ToArray();

        Assert.Contains("flow.start", nodeTypes);
        Assert.Contains("flow.end", nodeTypes);
        Assert.Contains("flow.delay", nodeTypes);
        Assert.Contains("flow.parallel", nodeTypes);
        Assert.Contains("flow.if", nodeTypes);
        Assert.Contains("action.log", nodeTypes);
        Assert.Contains("action.setProperty", nodeTypes);
        Assert.Contains("action.resetProperty", nodeTypes);
        Assert.Contains("action.animateProperty", nodeTypes);
        Assert.Contains("value.number", nodeTypes);
        Assert.Contains("value.string", nodeTypes);
        Assert.Contains("value.boolean", nodeTypes);
        Assert.Contains("value.color", nodeTypes);
        Assert.Contains("value.eventValue", nodeTypes);
        Assert.Contains("value.selfTag", nodeTypes);
        Assert.Contains("value.controlReference", nodeTypes);
    }

    [Fact]
    public void NodeCatalog_StartNode_HasOutPort()
    {
        var start = new FrontedNodeCatalog().Find("flow.start")!;

        Assert.Contains(start.OutputPorts, port => port.Name == "Out");
    }

    [Fact]
    public void NodeCatalog_IfNode_HasTrueFalseOutputs()
    {
        var node = new FrontedNodeCatalog().Find("flow.if")!;

        Assert.Contains(node.OutputPorts, port => port.Name == "True");
        Assert.Contains(node.OutputPorts, port => port.Name == "False");
    }

    [Fact]
    public void NodeCatalog_AnimateProperty_HasRequiredProperties()
    {
        var node = new FrontedNodeCatalog().Find("action.animateProperty")!;

        Assert.Contains(node.Properties, property => property.Name == "PropertyName" && property.IsRequired);
        Assert.Contains(node.Properties, property => property.Name == "DurationMs" && property.IsRequired);
    }

    [Fact]
    public void VisibilityProperty_UsesEnumOptions()
    {
        Assert.Equal(["Visible", "Hidden", "Collapsed"], FrontedBehaviorPropertyMetadata.VisibilityOptions);
    }

    [Fact]
    public void PropertyNameEditor_ProvidesCommonOptions()
    {
        var node = new FrontedNodeCatalog().Find("action.animateProperty")!;
        var property = node.Properties.Single(property => property.Name == "PropertyName");

        Assert.Equal(FrontedNodePropertyEditorKind.PropertyName, property.EditorKind);
        Assert.Contains("Opacity", property.Options);
        Assert.Contains("FillColor", property.Options);
    }
}
