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
        Assert.InRange(propertyChangedCount, 1, 20);
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
