using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public void RotationProperty_ValidatesFiniteNumber()
    {
        var editor = CreateEditorWithNodes("action.animateProperty");
        editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName").TextValue = "Rotation";
        var to = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "To");

        to.TextValue = "NaN";

        Assert.True(to.HasValidationError);
        Assert.NotEqual("NaN", editor.SelectedNode.Model.Properties["To"].GetString());
    }

    [Fact]
    public void OpacityProperty_ValidatesRange()
    {
        var editor = CreateEditorWithNodes("action.animateProperty");
        editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName").TextValue = "Opacity";
        var to = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "To");

        to.TextValue = "2";

        Assert.True(to.HasValidationError);
    }

    [Fact]
    public void ColorProperty_AcceptsNamedColor()
    {
        var editor = CreateEditorWithNodes("action.setProperty");
        editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName").TextValue = "FillColor";
        var value = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "Value");

        value.TextValue = "White";

        Assert.False(value.HasValidationError);
        Assert.Equal("#FFFFFFFF", editor.SelectedNode.Model.Properties["Value"].GetString());
    }

    [Fact]
    public void PropertyNameText_UpdatesDynamicValueEditorImmediately()
    {
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            localize: (key, fallback) => key == "Designer.Property.FillColor" ? "Fill Color Localized" : fallback);
        editor.AddNode("action.animateProperty");
        var propertyName = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName");
        var to = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "To");

        propertyName.PropertyNameText = "Fill Color Localized";

        Assert.Equal("FillColor", editor.SelectedNode.Model.Properties["PropertyName"].GetString());
        Assert.True(to.IsColor);
        Assert.False(to.IsText);
    }

    [Fact]
    public void PropertyAndEasingOptions_ExposeLocalizedDisplayNames()
    {
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            localize: (key, fallback) => key switch
            {
                "Designer.Property.Opacity" => "Opacity Localized",
                "Designer.Option.Easing.SineInOut" => "Sine In Out Localized",
                _ => fallback
            });
        editor.AddNode("action.animateProperty");
        var propertyName = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName");
        var easing = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "Easing");

        Assert.Contains(propertyName.LocalizedOptions, option => option.Value == "Opacity" && option.DisplayName == "Opacity Localized");
        Assert.Contains(easing.LocalizedOptions, option => option.Value == "SineInOut" && option.DisplayName == "Sine In Out Localized");

        easing.SuggestionText = "Sine In Out Localized";

        Assert.Equal("SineInOut", editor.SelectedNode.Model.Properties["Easing"].GetString());
    }

    [Fact]
    public void TargetEditor_StoresSelfOrGuidReference()
    {
        var targetGuid = Guid.NewGuid();
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            targetOptions:
            [
                new FrontedNodeTargetOptionViewModel("Self", "Self"),
                new FrontedNodeTargetOptionViewModel($"guid:{targetGuid}", "Target")
            ]);
        editor.AddNode("action.setProperty");
        var target = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "Target");

        target.TargetValue = $"guid:{targetGuid}";

        Assert.Equal($"guid:{targetGuid}", editor.SelectedNode.Model.Properties["Target"].GetString());
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

    [Fact]
    public async Task GraphPreview_NoTargetScope_LogsWarningAndDoesNotCrash()
    {
        var catalog = new FrontedNodeCatalog();
        var graph = new FrontedNodeGraph();
        var start = catalog.CreateNode("flow.start");
        var end = catalog.CreateNode("flow.end");
        graph.Nodes.AddRange([start, end]);
        graph.Connections.Add(new FrontedNodeConnection { SourceNodeId = start.NodeId, SourcePort = "Out", TargetNodeId = end.NodeId, TargetPort = "In" });
        var editor = new FrontedNodeGraphEditorViewModel(
            graph,
            catalog,
            runtime: new FrontedNodeGraphRuntime(catalog, new FrontedNodeGraphValidator(catalog)),
            animationRuntime: new FrontedAnimationRuntime(),
            createAnimationContext: () => null);

        await editor.RunGraphPreviewAsync();

        Assert.Contains(editor.ExecutionLog, item => item.Message == "No preview target scope available.");
    }

    [Fact]
    public async Task LoopPreview_StartLoopStop_ExecutesGraphs()
    {
        var runtime = new RecordingGraphRuntime();
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            StartGraph = new FrontedNodeGraph(),
            LoopGraph = new FrontedNodeGraph(),
            StopGraph = new FrontedNodeGraph(),
            LoopPolicy = new FrontedLoopPolicy { RepeatCount = 1, StopMode = FrontedLoopStopMode.RunStopGraph, ResetOnStop = false }
        };
        var editor = new FrontedBehaviorAnimationEditorViewModel(
            behavior,
            (_, fallback) => fallback,
            runtime: runtime);

        await editor.PreviewStartCommand.ExecuteAsync(null);
        await editor.PreviewLoopOnceCommand.ExecuteAsync(null);
        await editor.PreviewStopCommand.ExecuteAsync(null);

        Assert.Equal([behavior.StartGraph, behavior.LoopGraph, behavior.StopGraph], runtime.Graphs);
    }

    private static FrontedNodeGraphEditorViewModel CreateEditorWithNodes(params string[] nodeTypes)
    {
        var catalog = new FrontedNodeCatalog();
        var graph = new FrontedNodeGraph { Nodes = nodeTypes.Select((type, index) => catalog.CreateNode(type, 40 + index * 100, 40)).ToList() };
        var editor = new FrontedNodeGraphEditorViewModel(graph, catalog);
        editor.SelectedNode = editor.Nodes.FirstOrDefault();
        return editor;
    }

    private sealed class RecordingGraphRuntime : IFrontedNodeGraphRuntime
    {
        public List<FrontedNodeGraph> Graphs { get; } = [];

        public Task<FrontedGraphExecutionResult> ExecuteAsync(
            FrontedNodeGraph graph,
            FrontedGraphExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Graphs.Add(graph);
            return Task.FromResult(new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success });
        }
    }
}
