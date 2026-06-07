using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

public class FrontedNodeGraphEditorViewModelTest
{
    [Fact]
    public void GraphEditor_AddNode_AddsModelNodeWithPosition()
    {
        var graph = new FrontedNodeGraph();
        var editor = new FrontedNodeGraphEditorViewModel(graph);

        editor.AddNode("flow.start");

        var node = Assert.Single(graph.Nodes);
        Assert.Equal("flow.start", node.NodeType);
        Assert.Equal(40, node.X);
        Assert.Equal(40, node.Y);
    }

    [Fact]
    public void GraphEditor_MoveNode_UpdatesModelXY()
    {
        var editor = CreateEditorWithNodes("flow.start");
        var node = Assert.Single(editor.Nodes);

        editor.MoveNode(node, 120, 130);

        Assert.Equal(120, node.Model.X);
        Assert.Equal(130, node.Model.Y);
    }

    [Fact]
    public void GraphEditor_DeleteNode_RemovesConnections()
    {
        var editor = CreateEditorWithNodes("flow.start", "flow.end");
        editor.AddConnection(editor.Nodes[0].OutputPorts[0], editor.Nodes[1].InputPorts[0]);
        editor.SelectedNode = editor.Nodes[0];

        editor.DeleteSelectedNode();

        Assert.Empty(editor.Graph.Connections);
    }

    [Fact]
    public void GraphEditor_AddConnection_ValidPorts_AddsConnection()
    {
        var editor = CreateEditorWithNodes("flow.start", "flow.end");

        var added = editor.AddConnection(editor.Nodes[0].OutputPorts[0], editor.Nodes[1].InputPorts[0]);

        Assert.True(added);
        Assert.Single(editor.Graph.Connections);
    }

    [Fact]
    public void GraphEditor_AddConnection_InvalidPorts_Rejected()
    {
        var editor = CreateEditorWithNodes("flow.start", "flow.end");

        var added = editor.AddConnection(editor.Nodes[0].OutputPorts[0], editor.Nodes[0].OutputPorts[0]);

        Assert.False(added);
        Assert.Empty(editor.Graph.Connections);
    }

    [Fact]
    public void GraphEditor_EditNodeProperty_UpdatesJsonPropertyAndMarksDirty()
    {
        var dirty = 0;
        var editor = new FrontedNodeGraphEditorViewModel(new FrontedNodeGraph(), markDirty: () => dirty++);
        editor.AddNode("action.log");
        dirty = 0;

        editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "Message").TextValue = "hello";

        Assert.Equal("hello", editor.SelectedNode.Model.Properties["Message"].GetString());
        Assert.True(dirty > 0);
    }

    [Fact]
    public void GraphEditor_DuplicateNode_GeneratesNewNodeId()
    {
        var editor = CreateEditorWithNodes("flow.start");
        var originalId = editor.SelectedNode!.Model.NodeId;

        editor.DuplicateSelectedNode();

        Assert.Equal(2, editor.Graph.Nodes.Count);
        Assert.NotEqual(originalId, editor.SelectedNode!.Model.NodeId);
    }

    private static FrontedNodeGraphEditorViewModel CreateEditorWithNodes(params string[] nodeTypes)
    {
        var catalog = new FrontedNodeCatalog();
        var graph = new FrontedNodeGraph { Nodes = nodeTypes.Select((type, index) => catalog.CreateNode(type, 40 + index * 100, 40)).ToList() };
        var editor = new FrontedNodeGraphEditorViewModel(graph, catalog);
        editor.SelectedNode = editor.Nodes.FirstOrDefault();
        return editor;
    }
}
