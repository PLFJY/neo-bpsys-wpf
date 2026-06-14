using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
    public void GraphEditor_AddNode_CapturesOuterUndoBeforeMutation()
    {
        var graph = new FrontedNodeGraph();
        var capturedNodeCount = -1;
        var editor = new FrontedNodeGraphEditorViewModel(
            graph,
            captureUndoSnapshot: () => capturedNodeCount = graph.Nodes.Count);

        editor.AddNode("flow.start");

        Assert.Equal(0, capturedNodeCount);
        Assert.Single(graph.Nodes);
    }

    [Fact]
    public void BooleanNodeProperty_ExposesComboBoxChoices()
    {
        var catalog = new FrontedNodeCatalog();
        var descriptor = catalog.Find("action.animateProperty")!
            .Properties.Single(property => property.Name == "WaitForCompletion");
        var node = new FrontedNode
        {
            NodeType = "action.animateProperty"
        };
        var property = new FrontedNodePropertyEditorViewModel(
            node,
            descriptor,
            static () => { },
            static () => { },
            static (_, fallback) => fallback,
            []);

        Assert.True(property.IsBoolean);
        Assert.Equal(["true", "false"], property.BooleanOptions.Select(option => option.Value).ToArray());

        property.BooleanTextValue = "false";

        Assert.Equal("false", property.BooleanTextValue);
        Assert.False(node.Properties["WaitForCompletion"].GetBoolean());
    }

    [Fact]
    public void TransitionCharacterPickCondition_UsesCatalogFieldsAndTypedValues()
    {
        var catalog = new FrontedNodeCatalog();
        var ifNode = catalog.CreateNode("flow.if");
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Transition,
            TransitionTrigger = new TriggerDescriptor { EventType = "Selection.CharacterPick" },
            ExitGraph = new FrontedNodeGraph { Nodes = [ifNode] }
        };
        var editor = new FrontedBehaviorAnimationEditorViewModel(
            behavior,
            static (_, fallback) => fallback,
            catalog: catalog);
        var graphEditor = editor.Stages[0].GraphEditor;
        var node = Assert.Single(graphEditor.Nodes);
        var field = node.Properties.Single(property => property.Descriptor.Name == "Left");
        var value = node.Properties.Single(property => property.Descriptor.Name == "Right");

        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "Event.HasOldCharacter");
        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "Event.HasNewCharacter");
        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "Event.Camp");
        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "Event.PlayerIndex");
        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "Event.OldCharacterId");
        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "Event.NewCharacterId");

        field.ConditionFieldValue = "Event.HasOldCharacter";
        Assert.Equal("Event.HasOldCharacter", ifNode.Properties["Left"].GetString());
        Assert.True(value.IsBooleanConditionValue);
        Assert.Equal(["true", "false"], value.ConditionValueOptions.Select(option => option.Value).ToArray());

        field.ConditionFieldValue = "Event.Camp";
        Assert.True(value.IsEnumConditionValue);
        Assert.Equal(["Sur", "Hun"], value.ConditionValueOptions.Select(option => option.Value).ToArray());
        value.ConditionChoiceValue = "Sur";
        Assert.Equal("Sur", ifNode.Properties["Right"].GetString());
    }

    [Fact]
    public void TransitionCharacterPickCondition_LocalizesDisplayButPersistsStablePath()
    {
        var catalog = new FrontedNodeCatalog();
        var ifNode = catalog.CreateNode("flow.if");
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Transition,
            TransitionTrigger = new TriggerDescriptor { EventType = "Selection.CharacterPick" },
            ExitGraph = new FrontedNodeGraph { Nodes = [ifNode] }
        };
        var editor = new FrontedBehaviorAnimationEditorViewModel(
            behavior,
            static (key, fallback) => key == "Designer.Behaviors.Payload.HasOldCharacter"
                ? "是否存在上一个角色"
                : fallback,
            catalog: catalog);
        var node = Assert.Single(editor.Stages[0].GraphEditor.Nodes);
        var field = node.Properties.Single(property => property.Descriptor.Name == "Left");
        var option = Assert.Single(field.ConditionFieldOptions, item => item.ValuePath == "Event.HasOldCharacter");

        Assert.Equal("是否存在上一个角色 (Event.HasOldCharacter)", option.DisplayText);
        Assert.DoesNotContain("Designer.Behaviors.Payload", option.DisplayText, StringComparison.Ordinal);

        field.ConditionFieldValue = option.ValuePath;
        node.Properties.Single(property => property.Descriptor.Name == "Right").ConditionChoiceValue = "true";

        Assert.Equal("Event.HasOldCharacter", ifNode.Properties["Left"].GetString());
        Assert.Contains("是否存在上一个角色", node.Summary, StringComparison.Ordinal);
        Assert.Equal("Event.HasOldCharacter Equals true", node.RawSummary);
    }

    [Fact]
    public void LoopStopCondition_OffersStopEventUnionAndStartEventFields()
    {
        var catalog = new FrontedNodeCatalog();
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            StartTrigger = new TriggerDescriptor { EventType = "Guidance.StepChanged" },
            StopTriggers =
            [
                new TriggerDescriptor { EventType = "Guidance.Cancelled" },
                new TriggerDescriptor { EventType = "Guidance.Stopped" }
            ],
            StopGraph = new FrontedNodeGraph { Nodes = [catalog.CreateNode("flow.if")] }
        };
        var editor = new FrontedBehaviorAnimationEditorViewModel(
            behavior,
            static (_, fallback) => fallback,
            catalog: catalog);
        var field = editor.Stages[2].GraphEditor.Nodes.Single().Properties
            .Single(property => property.Descriptor.Name == "Left");

        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "StartEvent.Action");
        Assert.Contains(field.ConditionFieldOptions, option => option.ValuePath == "Event.Reason"
            && option.DisplayText.Contains("Guidance.Cancelled", StringComparison.Ordinal));
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
    public void GraphEditor_AddNode_SkipsSecondEndNode()
    {
        var editor = CreateEditorWithNodes("flow.end");

        editor.AddNode("flow.end");

        Assert.Single(editor.Graph.Nodes);
        Assert.Single(editor.Graph.Nodes, node => node.NodeType == "flow.end");
    }

    [Fact]
    public void GraphEditor_AddNode_SkipsSecondStartNode()
    {
        var editor = CreateEditorWithNodes("flow.start");

        editor.AddNode("flow.start");

        Assert.Single(editor.Graph.Nodes);
        Assert.Single(editor.Graph.Nodes, node => node.NodeType == "flow.start");
    }

    [Fact]
    public void GraphEditor_StartCompleteConnection_AddsConnection()
    {
        var editor = CreateEditorWithNodes("flow.start", "flow.end");

        editor.StartConnection(editor.Nodes[0].OutputPorts[0]);
        editor.CompleteConnection(editor.Nodes[1].InputPorts[0]);

        Assert.Single(editor.Graph.Connections);
        Assert.False(editor.IsConnecting);
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
    public void GraphEditor_ReverseConnection_NormalizesToOutputAndInput()
    {
        var editor = CreateEditorWithNodes("flow.start", "flow.end");

        var added = editor.AddConnection(editor.Nodes[1].InputPorts[0], editor.Nodes[0].OutputPorts[0]);

        Assert.True(added);
        var connection = Assert.Single(editor.Graph.Connections);
        Assert.Equal(editor.Nodes[0].Model.NodeId, connection.SourceNodeId);
        Assert.Equal(editor.Nodes[1].Model.NodeId, connection.TargetNodeId);
    }

    [Fact]
    public void GraphEditor_NewOutgoingConnection_ReplacesExistingConnection()
    {
        var editor = CreateEditorWithNodes("flow.start", "action.log", "flow.end");
        var source = editor.Nodes[0].OutputPorts[0];
        editor.AddConnection(source, editor.Nodes[1].InputPorts[0]);

        editor.AddConnection(source, editor.Nodes[2].InputPorts[0]);

        var connection = Assert.Single(editor.Graph.Connections);
        Assert.Equal(editor.Nodes[2].Model.NodeId, connection.TargetNodeId);
    }

    [Fact]
    public void GraphEditor_EndInput_AllowsMultipleIncomingConnections()
    {
        var editor = CreateEditorWithNodes("flow.if", "flow.end");
        var condition = editor.Nodes[0];
        var endInput = editor.Nodes[1].InputPorts[0];

        var trueAdded = editor.AddConnection(condition.OutputPorts.Single(port => port.Name == "True"), endInput);
        var falseAdded = editor.AddConnection(condition.OutputPorts.Single(port => port.Name == "False"), endInput);

        Assert.True(trueAdded);
        Assert.True(falseAdded);
        Assert.Equal(2, editor.Graph.Connections.Count);
        Assert.All(editor.Graph.Connections, connection => Assert.Equal(editor.Nodes[1].Model.NodeId, connection.TargetNodeId));
    }

    [Fact]
    public void ParallelNode_OutputPorts_ExposeLocalizedLabelsAndRoles()
    {
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            localize: (key, fallback) => key switch
            {
                "Designer.Graph.Port.Branch1" => "分支 1",
                "Designer.Graph.Port.Branch2" => "分支 2",
                "Designer.Graph.Port.Branch3" => "分支 3",
                "Designer.Graph.Port.ParallelOut" => "全部完成后",
                "Designer.Graph.Port.ParallelOut.Tooltip" => "所有已连接的并行分支执行完成后，从这里继续。",
                _ => fallback
            });

        editor.AddNode("flow.parallel");

        var ports = editor.SelectedNode!.OutputPorts;
        Assert.Equal(["分支 1", "分支 2", "分支 3", "全部完成后"], ports.Select(port => port.DisplayName).ToArray());
        Assert.All(ports.Take(3), port => Assert.Equal(FrontedNodePortRole.ParallelBranch, port.Role));
        var continuation = ports.Single(port => port.Name == "Out");
        Assert.Equal(FrontedNodePortRole.ParallelContinuation, continuation.Role);
        Assert.True(continuation.CenterOffsetY > ports.Single(port => port.Name == "Branch3").CenterOffsetY + 24);
        Assert.Contains("所有已连接的并行分支执行完成后", continuation.TooltipText);
    }

    [Fact]
    public void ParallelConnections_UseSourcePortRoleStyleAndMeaning()
    {
        var editor = CreateEditorWithNodes("flow.parallel", "action.log", "flow.end");
        var parallel = editor.Nodes[0];
        var log = editor.Nodes[1];
        var end = editor.Nodes[2];
        editor.AddConnection(parallel.OutputPorts.Single(port => port.Name == "Branch1"), log.InputPorts[0]);
        editor.AddConnection(parallel.OutputPorts.Single(port => port.Name == "Out"), end.InputPorts[0]);

        var branchConnection = editor.Connections.Single(connection => connection.Model.SourcePort == "Branch1");
        var continuationConnection = editor.Connections.Single(connection => connection.Model.SourcePort == "Out");

        Assert.Equal("#1976D2", branchConnection.StrokeColorHex);
        Assert.Equal("#8BC34A", continuationConnection.StrokeColorHex);
        Assert.True(continuationConnection.StrokeThickness > branchConnection.StrokeThickness);
        Assert.Contains("所有并行分支完成后继续", continuationConnection.Meaning);
    }

    [Fact]
    public void ParallelNode_BranchCountProperty_RebuildsVisiblePorts()
    {
        var editor = CreateEditorWithNodes("flow.parallel");
        var branchCount = editor.Nodes[0].Properties.Single(property => property.Descriptor.Name == "BranchCount");

        branchCount.TextValue = "20";

        var parallel = Assert.Single(editor.Nodes);
        Assert.Equal(21, parallel.OutputPorts.Count);
        Assert.Contains(parallel.OutputPorts, port => port.Name == "Branch20");
        Assert.Equal("20", parallel.Model.Properties["BranchCount"].ToString());
    }

    [Fact]
    public void ParallelNode_BranchCountUsesObservableValidator()
    {
        var editor = CreateEditorWithNodes("flow.parallel");
        var branchCount = editor.Nodes[0].Properties.Single(property => property.Descriptor.Name == "BranchCount");

        branchCount.NumberValue = 21;

        Assert.True(branchCount.HasErrors);
        Assert.True(branchCount.HasValidationError);
        Assert.NotEmpty(branchCount.GetErrors(nameof(branchCount.NumberValue)));
        Assert.Equal("3", branchCount.TextValue);

        branchCount.NumberValue = 4;

        Assert.False(branchCount.HasErrors);
        Assert.Equal("4", branchCount.TextValue);
    }

    [Fact]
    public void ParallelNode_ReducingBranchCount_RemovesHiddenConnections()
    {
        var catalog = new FrontedNodeCatalog();
        var parallel = catalog.CreateNode("flow.parallel");
        parallel.Properties["BranchCount"] = JsonSerializer.SerializeToElement(5);
        var log = catalog.CreateNode("action.log");
        var graph = new FrontedNodeGraph
        {
            Nodes = [parallel, log],
            Connections =
            [
                new FrontedNodeConnection
                {
                    SourceNodeId = parallel.NodeId,
                    SourcePort = "Branch5",
                    TargetNodeId = log.NodeId,
                    TargetPort = "In"
                }
            ]
        };
        var editor = new FrontedNodeGraphEditorViewModel(graph, catalog);

        editor.Nodes.Single(node => node.Model.NodeId == parallel.NodeId)
            .Properties.Single(property => property.Descriptor.Name == "BranchCount")
            .TextValue = "3";

        Assert.DoesNotContain(graph.Connections, connection => connection.SourcePort == "Branch5");
        Assert.DoesNotContain(editor.Nodes.Single(node => node.Model.NodeId == parallel.NodeId).OutputPorts, port => port.Name == "Branch5");
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
    public void PropertyNameBindingFeedback_DoesNotRewriteSameValue()
    {
        var dirty = 0;
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            markDirty: () => dirty++,
            localize: (key, fallback) => key == "Designer.Property.Opacity" ? "Opacity Localized" : fallback);
        editor.AddNode("action.animateProperty");
        var propertyName = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName");
        dirty = 0;
        var propertyChangedCount = 0;
        propertyName.PropertyChanged += (_, args) =>
        {
            propertyChangedCount++;
            if (args.PropertyName == nameof(propertyName.PropertyNameValue))
            {
                propertyName.PropertyNameText = propertyName.PropertyNameText;
            }
            else if (args.PropertyName == nameof(propertyName.PropertyNameText))
            {
                propertyName.PropertyNameValue = propertyName.PropertyNameValue;
            }
        };

        propertyName.PropertyNameValue = "Opacity";

        Assert.Equal("Opacity", editor.SelectedNode.Model.Properties["PropertyName"].GetString());
        Assert.Equal(1, dirty);
        Assert.InRange(propertyChangedCount, 1, 25);
    }

    [Fact]
    public void PropertyAndEasingOptions_ExposeLocalizedDisplayNames()
    {
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            localize: (key, fallback) => key switch
            {
                "Designer.Property.Opacity" => "Opacity Localized",
                "Designer.Property.ClipInsetRight" => "Clip Inset Right Localized",
                "Designer.Option.Easing.SineInOut" => "Sine In Out Localized",
                _ => fallback
            });
        editor.AddNode("action.animateProperty");
        var propertyName = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName");
        var easing = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "Easing");

        Assert.Contains(propertyName.LocalizedOptions, option => option.Value == "Opacity" && option.DisplayName == "Opacity Localized");
        Assert.Contains(propertyName.LocalizedOptions, option => option.Value == "ClipInsetRight" && option.DisplayName == "Clip Inset Right Localized");
        Assert.Contains(easing.LocalizedOptions, option => option.Value == "SineInOut" && option.DisplayName == "Sine In Out Localized");

        easing.SuggestionText = "Sine In Out Localized";

        Assert.Equal("SineInOut", editor.SelectedNode.Model.Properties["Easing"].GetString());
    }

    [Fact]
    public void ActionNodes_DefaultTargetLayerAuto()
    {
        foreach (var nodeType in new[] { "action.setProperty", "action.animateProperty", "action.resetProperty" })
        {
            var editor = CreateEditorWithNodes(nodeType);

            Assert.Equal("Auto", editor.SelectedNode!.Model.Properties["TargetLayer"].GetString());
        }
    }

    [Fact]
    public void PropertyNameOptions_FollowTargetLayer()
    {
        var editor = CreateEditorWithNodes("action.animateProperty");
        var layer = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "TargetLayer");
        var propertyName = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "PropertyName");

        layer.EnumValue = FrontedAnimationTargetLayer.Control.ToString();
        Assert.Contains(propertyName.DisplayedOptions, option => option.Value == "Opacity");
        Assert.DoesNotContain(propertyName.DisplayedOptions, option => option.Value == "StrokeColor");

        layer.EnumValue = FrontedAnimationTargetLayer.OverlayAbove.ToString();
        Assert.Contains(propertyName.DisplayedOptions, option => option.Value == "StrokeColor");
        Assert.DoesNotContain(propertyName.DisplayedOptions, option => option.Value == "TextColor");

        layer.EnumValue = FrontedAnimationTargetLayer.Content.ToString();
        Assert.Contains(propertyName.DisplayedOptions, option => option.Value == "TextColor");
        Assert.Contains(propertyName.DisplayedOptions, option => option.Value == "ClipInsetRight");
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
        var editor = CreateEditorWithNodes("action.log");
        var originalId = editor.SelectedNode!.Model.NodeId;

        editor.DuplicateSelectedNode();

        Assert.Equal(2, editor.Graph.Nodes.Count);
        Assert.NotEqual(originalId, editor.SelectedNode!.Model.NodeId);
    }

    [Fact]
    public void GraphEditor_DuplicateEndNode_SkipsSecondEnd()
    {
        var editor = CreateEditorWithNodes("flow.end");

        editor.DuplicateSelectedNode();

        Assert.Single(editor.Graph.Nodes);
        Assert.Single(editor.Graph.Nodes, node => node.NodeType == "flow.end");
    }

    [Fact]
    public void GraphEditor_DuplicateStartNode_SkipsSecondStart()
    {
        var editor = CreateEditorWithNodes("flow.start");

        editor.DuplicateSelectedNode();

        Assert.Single(editor.Graph.Nodes);
        Assert.Single(editor.Graph.Nodes, node => node.NodeType == "flow.start");
    }

    [Fact]
    public void GraphEditor_CopyPaste_PreservesInternalConnectionsAndRemapsIdsAcrossEditors()
    {
        FrontedNodeGraphClipboard.Payload = null;
        var source = CreateEditorWithNodes("flow.start", "flow.end");
        source.AddConnection(source.Nodes[0].OutputPorts[0], source.Nodes[1].InputPorts[0]);
        source.SelectNodes(new Rect(0, 0, 1000, 1000));
        source.CopySelectedNodes();
        var target = new FrontedNodeGraphEditorViewModel(new FrontedNodeGraph());

        target.PasteNodes();

        Assert.Equal(2, target.Graph.Nodes.Count);
        var connection = Assert.Single(target.Graph.Connections);
        Assert.Contains(target.Graph.Nodes, node => node.NodeId == connection.SourceNodeId);
        Assert.Contains(target.Graph.Nodes, node => node.NodeId == connection.TargetNodeId);
        Assert.DoesNotContain(target.Graph.Nodes, node => source.Graph.Nodes.Any(original => original.NodeId == node.NodeId));
        Assert.All(target.Graph.Nodes, node => Assert.True(node.X >= 32 && node.Y >= 32));
    }

    [Fact]
    public void GraphEditor_PasteEndIntoGraphWithEnd_SkipsEndAndItsConnections()
    {
        FrontedNodeGraphClipboard.Payload = null;
        var source = CreateEditorWithNodes("flow.start", "flow.end");
        source.AddConnection(source.Nodes[0].OutputPorts[0], source.Nodes[1].InputPorts[0]);
        source.SelectNodes(new Rect(0, 0, 1000, 1000));
        source.CopySelectedNodes();
        var target = CreateEditorWithNodes("flow.end");

        target.PasteNodes();

        Assert.Equal(2, target.Graph.Nodes.Count);
        Assert.Single(target.Graph.Nodes, node => node.NodeType == "flow.end");
        Assert.Single(target.Graph.Nodes, node => node.NodeType == "flow.start");
        Assert.Empty(target.Graph.Connections);
    }

    [Fact]
    public void GraphEditor_PasteStartIntoGraphWithStart_SkipsStartAndItsConnections()
    {
        FrontedNodeGraphClipboard.Payload = null;
        var source = CreateEditorWithNodes("flow.start", "flow.end");
        source.AddConnection(source.Nodes[0].OutputPorts[0], source.Nodes[1].InputPorts[0]);
        source.SelectNodes(new Rect(0, 0, 1000, 1000));
        source.CopySelectedNodes();
        var target = CreateEditorWithNodes("flow.start");

        target.PasteNodes();

        Assert.Equal(2, target.Graph.Nodes.Count);
        Assert.Single(target.Graph.Nodes, node => node.NodeType == "flow.start");
        Assert.Single(target.Graph.Nodes, node => node.NodeType == "flow.end");
        Assert.Empty(target.Graph.Connections);
    }

    [Fact]
    public void GraphEditor_Constructor_RemovesDuplicateStartAndEndNodesWithConnections()
    {
        var catalog = new FrontedNodeCatalog();
        var start = catalog.CreateNode("flow.start");
        var extraStart = catalog.CreateNode("flow.start");
        var end = catalog.CreateNode("flow.end");
        var extraEnd = catalog.CreateNode("flow.end");
        var log = catalog.CreateNode("action.log");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, extraStart, end, extraEnd, log],
            Connections =
            [
                new FrontedNodeConnection { SourceNodeId = start.NodeId, SourcePort = "Out", TargetNodeId = end.NodeId, TargetPort = "In" },
                new FrontedNodeConnection { SourceNodeId = extraStart.NodeId, SourcePort = "Out", TargetNodeId = log.NodeId, TargetPort = "In" },
                new FrontedNodeConnection { SourceNodeId = log.NodeId, SourcePort = "Out", TargetNodeId = extraEnd.NodeId, TargetPort = "In" }
            ]
        };

        var editor = new FrontedNodeGraphEditorViewModel(graph, catalog);

        Assert.Single(editor.Graph.Nodes, node => node.NodeType == "flow.start");
        Assert.Single(editor.Graph.Nodes, node => node.NodeType == "flow.end");
        Assert.Equal(3, editor.Graph.Nodes.Count);
        var connection = Assert.Single(editor.Graph.Connections);
        Assert.Equal(start.NodeId, connection.SourceNodeId);
        Assert.Equal(end.NodeId, connection.TargetNodeId);
    }

    [Fact]
    public void DynamicPropertyMetadata_ProvidesHintsAndEditors()
    {
        var editor = CreateEditorWithNodes("action.animateProperty");
        var propertyName = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName");
        var to = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "To");

        propertyName.TextValue = "Opacity";
        Assert.Equal("0.0 - 1.0", to.Placeholder);

        propertyName.TextValue = "Visibility";
        Assert.True(to.IsVisibilityValue);
        Assert.Contains(to.VisibilityOptions, option => option.Value == "Collapsed");

        propertyName.TextValue = "FillColor";
        Assert.True(to.IsColor);
        Assert.Equal("#AARRGGBB or #RRGGBB", to.Placeholder);

        propertyName.TextValue = "ScaleX";
        Assert.Contains("normal size", to.Placeholder);
    }

    [Fact]
    public void DynamicNumericEditor_AcceptsPercentageForClipInset()
    {
        var editor = CreateEditorWithNodes("action.animateProperty");
        var propertyName = editor.SelectedNode!.Properties.Single(property => property.Descriptor.Name == "PropertyName");
        var to = editor.SelectedNode.Properties.Single(property => property.Descriptor.Name == "To");

        propertyName.TextValue = "ClipInsetRight";
        to.TextValue = "100%";

        Assert.False(to.HasValidationError);
        Assert.Equal("100%", editor.SelectedNode.Model.Properties["To"].GetString());
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
    public async Task GraphPreview_MissingEventContext_LogsLocalizedWarning()
    {
        var catalog = new FrontedNodeCatalog();
        var graph = IfGraph(catalog, "Event.HasOldCharacter");
        var editor = new FrontedNodeGraphEditorViewModel(
            graph,
            catalog,
            localize: static (key, fallback) => key == "Designer.Graph.Preview.MissingEventContext"
                ? "当前预览没有事件上下文，{0} 将无法解析。"
                : fallback);

        await editor.RunGraphPreviewAsync();

        Assert.Contains(editor.ExecutionLog, item =>
            item.Message == "当前预览没有事件上下文，Event.HasOldCharacter 将无法解析。");
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

    [Fact]
    public async Task LoopPreview_StartLoop_WithZeroInterval_CompletesWithUiFriendlyTick()
    {
        var runtime = new RecordingGraphRuntime();
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            StartGraph = new FrontedNodeGraph(),
            LoopGraph = new FrontedNodeGraph(),
            StopGraph = new FrontedNodeGraph(),
            LoopPolicy = new FrontedLoopPolicy { RepeatCount = 3, IntervalMs = 0 }
        };
        var editor = new FrontedBehaviorAnimationEditorViewModel(
            behavior,
            (_, fallback) => fallback,
            runtime: runtime);

        await editor.StartLoopPreviewCommand.ExecuteAsync(null);

        Assert.False(editor.IsLoopPreviewRunning);
        Assert.Equal(1, runtime.Graphs.Count(graph => ReferenceEquals(graph, behavior.StartGraph)));
        Assert.Equal(3, runtime.Graphs.Count(graph => ReferenceEquals(graph, behavior.LoopGraph)));
    }

    [Fact]
    public async Task GraphEditor_SaveAsync_ClearsDirtyAfterAsyncSaveSucceeds()
    {
        var saveCalled = false;
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            saveAsync: async () =>
            {
                await Task.Yield();
                saveCalled = true;
                return true;
            });
        editor.IsDirty = true;

        await editor.SaveAsync();

        Assert.True(saveCalled);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public async Task GraphEditor_SaveAsync_KeepsDirtyWhenAsyncSaveFails()
    {
        var editor = new FrontedNodeGraphEditorViewModel(
            new FrontedNodeGraph(),
            saveAsync: () => Task.FromResult(false));
        editor.IsDirty = true;

        await editor.SaveAsync();

        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task AnimationEditor_SaveAll_ClearsAllStageDirty()
    {
        var saveCalled = false;
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            StartGraph = new FrontedNodeGraph(),
            LoopGraph = new FrontedNodeGraph(),
            StopGraph = new FrontedNodeGraph(),
            LoopPolicy = new FrontedLoopPolicy()
        };
        var editor = new FrontedBehaviorAnimationEditorViewModel(
            behavior,
            (_, fallback) => fallback,
            saveAsync: () =>
            {
                saveCalled = true;
                return Task.FromResult(true);
            });

        // Mark all stages as dirty
        foreach (var stage in editor.Stages)
        {
            stage.GraphEditor.IsDirty = true;
        }
        Assert.True(editor.HasUnsavedChanges);

        var result = await editor.SaveAllAsync();

        Assert.True(result);
        Assert.True(saveCalled);
        Assert.False(editor.HasUnsavedChanges);
        foreach (var stage in editor.Stages)
        {
            Assert.False(stage.GraphEditor.IsDirty);
        }
    }

    [Fact]
    public async Task AnimationEditor_SaveFailed_KeepsDirty()
    {
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            StartGraph = new FrontedNodeGraph(),
            LoopGraph = new FrontedNodeGraph(),
            StopGraph = new FrontedNodeGraph(),
            LoopPolicy = new FrontedLoopPolicy()
        };
        var editor = new FrontedBehaviorAnimationEditorViewModel(
            behavior,
            (_, fallback) => fallback,
            saveAsync: () => Task.FromResult(false));

        // Mark all stages as dirty
        foreach (var stage in editor.Stages)
        {
            stage.GraphEditor.IsDirty = true;
        }

        var result = await editor.SaveAllAsync();

        Assert.False(result);
        // All stages should remain dirty since save failed
        foreach (var stage in editor.Stages)
        {
            Assert.True(stage.GraphEditor.IsDirty);
        }
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public async Task AnimationEditor_DiscardAll_ClearsDirtyAndResetsPreviewWithoutRunningStopGraph()
    {
        await RunOnStaThreadAsync(() =>
        {
            var graphRuntime = new RecordingGraphRuntime();
            var animationRuntime = new RecordingAnimationRuntime();
            var previewScope = new FrontedDesignerPreviewAnimationScope();
            previewScope.Update(new Grid(), null, "Window", "Canvas", []);
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy { StopMode = FrontedLoopStopMode.RunStopGraph }
            };
            var editor = new FrontedBehaviorAnimationEditorViewModel(
                behavior,
                (_, fallback) => fallback,
                runtime: graphRuntime,
                animationRuntime: animationRuntime,
                previewAnimationScope: previewScope);
            foreach (var stage in editor.Stages)
            {
                stage.GraphEditor.IsDirty = true;
            }

            editor.DiscardAll();

            Assert.False(editor.HasUnsavedChanges);
            Assert.Empty(graphRuntime.Graphs);
            Assert.Equal(1, animationRuntime.ResetAllCount);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PreviewAnimationScope_UsesConfigDrivenPartsWithoutRenderedChildren()
    {
        await RunOnStaThreadAsync(() =>
        {
            var guid = Guid.NewGuid();
            var root = new Grid();
            var scope = new FrontedDesignerPreviewAnimationScope();

            scope.Update(
                root,
                null,
                "Window",
                "Canvas",
                [new FrontedControlDesignItem
                {
                    Name = "BanSlot",
                    Config = new ImageFrontedControlConfig
                    {
                        BehaviorGuid = guid,
                        Lockable = true
                    }
                }]);

            Assert.Contains(scope.Targets, target => target.TargetReference == $"guid:{guid}");
            Assert.Contains(scope.Targets, target => target.TargetReference == $"part:{guid}:{FrontedAnimationPartNames.LockOverlay}");
            Assert.DoesNotContain(scope.Targets, target => target.TargetReference == $"part:{guid}:{FrontedAnimationPartNames.PickingBorder}");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PreviewAnimationScope_UsesConfiguredGenericAnimationPartTargets()
    {
        await RunOnStaThreadAsync(() =>
        {
            var guid = Guid.NewGuid();
            var scope = new FrontedDesignerPreviewAnimationScope();
            scope.Update(
                new Grid(),
                null,
                "Window",
                "Canvas",
                [new FrontedControlDesignItem
                {
                    Name = "SurPick0",
                    Config = new FrontedControlConfigBase
                    {
                        BehaviorGuid = guid
                    }
                }],
                new FrontedBehaviorDocument
                {
                    ControlBehaviorSets =
                    [
                        new ControlBehaviorSet
                        {
                            BehaviorGuid = guid,
                            DisplayName = "SurPick0",
                            AnimationParts = [new FrontedAnimationPartConfig { Name = "wipeBar" }]
                        }
                    ]
                });

            Assert.Contains(
                scope.Targets,
                target => target.TargetReference == $"part:{guid}:wipeBar"
                          && target.DisplayName == "SurPick0"
                          && target.PartName == "wipeBar");
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public async Task PreviewAnimationScope_ConfigFlagsControlBuiltInPartTargets(
        bool lockable,
        bool pickingBorderAvailable,
        bool expectsLockOverlay,
        bool expectsPickingBorder)
    {
        await RunOnStaThreadAsync(() =>
        {
            var guid = Guid.NewGuid();
            var scope = new FrontedDesignerPreviewAnimationScope();
            scope.Update(
                new Grid(),
                null,
                "Window",
                "Canvas",
                [new FrontedControlDesignItem
                {
                    Name = "Image",
                    Config = new ImageFrontedControlConfig
                    {
                        BehaviorGuid = guid,
                        Lockable = lockable,
                        PickingBorderAvailable = pickingBorderAvailable
                    }
                }]);

            Assert.Equal(
                expectsLockOverlay,
                scope.Targets.Any(target => target.TargetReference == $"part:{guid}:{FrontedAnimationPartNames.LockOverlay}"));
            Assert.Equal(
                expectsPickingBorder,
                scope.Targets.Any(target => target.TargetReference == $"part:{guid}:{FrontedAnimationPartNames.PickingBorder}"));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PreviewAnimationScope_DeduplicatesConfigAndVisualTreePartTargets()
    {
        await RunOnStaThreadAsync(() =>
        {
            var guid = Guid.NewGuid();
            var root = new Grid();
            var part = new Border();
            FrontedRendererProperties.SetIsAnimationAuxiliaryElement(part, true);
            FrontedRendererProperties.SetParentBehaviorGuid(part, guid);
            FrontedRendererProperties.SetParentRegisteredName(part, "BanSlot");
            FrontedRendererProperties.SetAnimationPartName(part, FrontedAnimationPartNames.LockOverlay);
            root.Children.Add(part);
            var scope = new FrontedDesignerPreviewAnimationScope();

            scope.Update(
                root,
                null,
                "Window",
                "Canvas",
                [new FrontedControlDesignItem
                {
                    Name = "BanSlot",
                    Config = new ImageFrontedControlConfig { BehaviorGuid = guid, Lockable = true }
                }]);

            Assert.Single(
                scope.Targets,
                target => target.TargetReference == $"part:{guid}:{FrontedAnimationPartNames.LockOverlay}");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnimationEditorOpenedAfterConfigChange_UsesLatestPreviewTargetOptions()
    {
        await RunOnStaThreadAsync(() =>
        {
            var guid = Guid.NewGuid();
            var scope = new FrontedDesignerPreviewAnimationScope();
            var panel = new BehaviorPanelViewModel(
                new FrontedDesignerLocalizationService(),
                new FrontedBehaviorEventCatalog(),
                static () => { },
                static () => { },
                previewAnimationScope: scope);
            var item = new FrontedControlDesignItem
            {
                Name = "Pick",
                Config = new ImageFrontedControlConfig { BehaviorGuid = guid }
            };
            panel.SetSelectedControl(item);
            panel.AddOneShotBehavior();

            scope.Update(new Grid(), item, "Window", "Canvas", [item]);
            var image = Assert.IsType<ImageFrontedControlConfig>(item.Config);
            image.PickingBorderAvailable = true;
            FrontedBehaviorAnimationEditorViewModel? editor = null;
            panel.AnimationEditorRequested += value => editor = value;

            panel.SelectedBehavior!.OpenAnimationEditorCommand.Execute(null);

            var graphEditor = Assert.Single(editor!.Stages).GraphEditor;
            graphEditor.AddNode("action.setProperty");
            var targets = graphEditor.SelectedNode!.Properties
                .Single(property => property.Descriptor.Name == "Target")
                .TargetOptions;
            Assert.Contains(
                targets,
                target => target.Value == $"part:{guid}:{FrontedAnimationPartNames.PickingBorder}"
                          && target.DisplayName == "Pick.PickingBorder");
            Assert.DoesNotContain(targets, target => target.Value == $"part:{guid}:{FrontedAnimationPartNames.LockOverlay}");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnimationEditor_StopLoopPreview_RunStopGraph_DoesNotResetAfterStopGraphSucceeds()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var graphRuntime = new RecordingGraphRuntime();
            var animationRuntime = new RecordingAnimationRuntime();
            var previewScope = new FrontedDesignerPreviewAnimationScope();
            previewScope.Update(new Grid(), null, "Window", "Canvas", []);
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = true
                }
            };
            var editor = new FrontedBehaviorAnimationEditorViewModel(
                behavior,
                (_, fallback) => fallback,
                runtime: graphRuntime,
                animationRuntime: animationRuntime,
                previewAnimationScope: previewScope);

            await editor.StopLoopPreviewCommand.ExecuteAsync(null);

            Assert.Contains(behavior.StopGraph, graphRuntime.Graphs);
            Assert.Equal(0, animationRuntime.ResetAllCount);
        });
    }

    private static FrontedNodeGraphEditorViewModel CreateEditorWithNodes(params string[] nodeTypes)
    {
        var catalog = new FrontedNodeCatalog();
        var graph = new FrontedNodeGraph { Nodes = nodeTypes.Select((type, index) => catalog.CreateNode(type, 40 + index * 100, 40)).ToList() };
        var editor = new FrontedNodeGraphEditorViewModel(graph, catalog);
        editor.SelectedNode = editor.Nodes.FirstOrDefault();
        return editor;
    }

    private static FrontedNodeGraph IfGraph(FrontedNodeCatalog catalog, string left)
    {
        var start = catalog.CreateNode("flow.start");
        var condition = catalog.CreateNode("flow.if");
        condition.Properties["Left"] = System.Text.Json.JsonSerializer.SerializeToElement(left);
        condition.Properties["Right"] = System.Text.Json.JsonSerializer.SerializeToElement("true");
        var end = catalog.CreateNode("flow.end");
        return new FrontedNodeGraph
        {
            Nodes = [start, condition, end],
            Connections =
            [
                new FrontedNodeConnection { SourceNodeId = start.NodeId, SourcePort = "Out", TargetNodeId = condition.NodeId, TargetPort = "In" },
                new FrontedNodeConnection { SourceNodeId = condition.NodeId, SourcePort = "False", TargetNodeId = end.NodeId, TargetPort = "In" }
            ]
        };
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

    private sealed class RecordingAnimationRuntime : IFrontedAnimationRuntime
    {
        public int ResetAllCount { get; private set; }

        public Task ExecuteAsync(
            IReadOnlyList<FrontedGraphActionRequest> actions,
            FrontedAnimationExecutionContext context,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ExecuteAsync(
            FrontedGraphActionRequest action,
            FrontedAnimationExecutionContext context,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void ResetTarget(Guid behaviorGuid, FrontedAnimationExecutionContext context)
        {
        }

        public void ResetAll(FrontedAnimationExecutionContext context)
        {
            ResetAllCount++;
        }

        public void Release(FrameworkElement root)
        {
        }
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        return WpfTestThread.RunAsync(action);
    }
}
