#nullable enable

using Microsoft.Extensions.Logging;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class FrontedTransitionOrchestratorTest
{
    [Fact]
    public async Task Transition_RunsExitCommitEnterInOrder()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetGuid = Guid.NewGuid();
            var exitGraph = new FrontedNodeGraph();
            var enterGraph = new FrontedNodeGraph();
            var order = new List<string>();
            var committed = false;

            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            graphRuntime
                .Setup(runtime => runtime.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FrontedNodeGraph graph, FrontedGraphExecutionContext _, CancellationToken _) =>
                {
                    if (ReferenceEquals(graph, exitGraph))
                    {
                        Assert.False(committed);
                        order.Add("exit");
                    }
                    else if (ReferenceEquals(graph, enterGraph))
                    {
                        Assert.True(committed);
                        order.Add("enter");
                    }

                    return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success };
                });

            var manager = await CreateAttachedManagerAsync(CreateDocument(targetGuid, exitGraph, enterGraph), graphRuntime.Object);
            var orchestrator = CreateOrchestrator(manager);

            await orchestrator.RunTransitionAsync(CreateRequest(targetGuid, 0), () =>
            {
                committed = true;
                order.Add("commit");
                return Task.CompletedTask;
            });

            Assert.Equal(["exit", "commit", "enter"], order);
        });
    }

    [Fact]
    public async Task Transition_NoMatchingBehavior_CommitsImmediately()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            var manager = await CreateAttachedManagerAsync(new FrontedBehaviorDocument(), graphRuntime.Object);
            var orchestrator = CreateOrchestrator(manager);
            var committed = false;

            await orchestrator.RunTransitionAsync(CreateRequest(Guid.NewGuid(), 0), () =>
            {
                committed = true;
                return Task.CompletedTask;
            });

            Assert.True(committed);
            graphRuntime.Verify(
                runtime => runtime.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        });
    }

    [Fact]
    public async Task Transition_TriggerFiltersMustMatchPayload()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetGuid = Guid.NewGuid();
            var exitGraph = new FrontedNodeGraph();
            var document = CreateDocument(targetGuid, exitGraph, new FrontedNodeGraph());
            document.ControlBehaviorSets[0].Behaviors[0].TransitionTrigger!.Filters.AddRange(
            [
                new TriggerFilter { Left = "Event.Camp", Right = "Sur" },
                new TriggerFilter { Left = "Event.PlayerIndex", Right = "0" }
            ]);

            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            graphRuntime
                .Setup(runtime => runtime.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success });

            var manager = await CreateAttachedManagerAsync(document, graphRuntime.Object);
            var orchestrator = CreateOrchestrator(manager);

            await orchestrator.RunTransitionAsync(CreateRequest(targetGuid, 1), () => Task.CompletedTask);

            graphRuntime.Verify(
                runtime => runtime.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        });
    }

    [Fact]
    public async Task Transition_ExitSeesOldDataAndEnterSeesNewData()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetGuid = Guid.NewGuid();
            var exitGraph = new FrontedNodeGraph();
            var enterGraph = new FrontedNodeGraph();
            var currentCharacter = "old";

            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            graphRuntime
                .Setup(runtime => runtime.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FrontedNodeGraph graph, FrontedGraphExecutionContext _, CancellationToken _) =>
                {
                    if (ReferenceEquals(graph, exitGraph))
                    {
                        Assert.Equal("old", currentCharacter);
                    }
                    else if (ReferenceEquals(graph, enterGraph))
                    {
                        Assert.Equal("new", currentCharacter);
                    }

                    return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success };
                });

            var manager = await CreateAttachedManagerAsync(CreateDocument(targetGuid, exitGraph, enterGraph), graphRuntime.Object);
            var orchestrator = CreateOrchestrator(manager);

            await orchestrator.RunTransitionAsync(CreateRequest(targetGuid, 0), () =>
            {
                currentCharacter = "new";
                return Task.CompletedTask;
            });
        });
    }

    [Fact]
    public async Task MultiTargetTransition_RunsAllExitsCommitThenAllEnters()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var firstGuid = Guid.NewGuid();
            var secondGuid = Guid.NewGuid();
            var firstExit = new FrontedNodeGraph();
            var firstEnter = new FrontedNodeGraph();
            var secondExit = new FrontedNodeGraph();
            var secondEnter = new FrontedNodeGraph();
            var committed = false;
            var exits = 0;
            var enters = 0;

            var document = new FrontedBehaviorDocument();
            document.ControlBehaviorSets.Add(CreateSet(firstGuid, firstExit, firstEnter, "Selection.CharacterSwap"));
            document.ControlBehaviorSets.Add(CreateSet(secondGuid, secondExit, secondEnter, "Selection.CharacterSwap"));

            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            graphRuntime
                .Setup(runtime => runtime.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((FrontedNodeGraph graph, FrontedGraphExecutionContext _, CancellationToken _) =>
                {
                    if (ReferenceEquals(graph, firstExit) || ReferenceEquals(graph, secondExit))
                    {
                        Assert.False(committed);
                        exits++;
                    }
                    else if (ReferenceEquals(graph, firstEnter) || ReferenceEquals(graph, secondEnter))
                    {
                        Assert.True(committed);
                        enters++;
                    }

                    return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success };
                });

            var manager = await CreateAttachedManagerAsync(document, graphRuntime.Object);
            var orchestrator = CreateOrchestrator(manager);

            await orchestrator.RunMultiTargetTransitionAsync(
                [CreateRequest(firstGuid, 0, "Selection.CharacterSwap"), CreateRequest(secondGuid, 1, "Selection.CharacterSwap")],
                () =>
                {
                    committed = true;
                    return Task.CompletedTask;
                });

            Assert.Equal(2, exits);
            Assert.Equal(2, enters);
        });
    }

    private static async Task<FrontedBehaviorRuntimeHostManager> CreateAttachedManagerAsync(
        FrontedBehaviorDocument document,
        IFrontedNodeGraphRuntime graphRuntime)
    {
        var behaviorService = new Mock<IFrontedBehaviorService>();
        behaviorService
            .Setup(service => service.LoadDocumentAsync("BpWindow", It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var manager = new FrontedBehaviorRuntimeHostManager(
            behaviorService.Object,
            new FrontedEventBus(),
            graphRuntime,
            Mock.Of<IFrontedAnimationRuntime>(),
            Mock.Of<IFrontedBehaviorAnimationPartRenderer>(),
            new FrontedBehaviorTriggerEvaluator(),
            Mock.Of<ILogger<FrontedBehaviorRuntimeHostManager>>());

        await manager.AttachHostAsync(new FrontedBehaviorRuntimeContext
        {
            WindowId = "TestWindow",
            WindowType = "BpWindow",
            RootCanvas = new Canvas(),
            WindowConfig = FrontedWindowConfig.FromCanvasConfig(new FrontedCanvasConfig()),
            SharedDataService = Mock.Of<ISharedDataService>()
        });

        return manager;
    }

    private static FrontedTransitionOrchestrator CreateOrchestrator(FrontedBehaviorRuntimeHostManager manager) =>
        new(manager, Mock.Of<ILogger<FrontedTransitionOrchestrator>>());

    private static FrontedBehaviorDocument CreateDocument(
        Guid targetGuid,
        FrontedNodeGraph exitGraph,
        FrontedNodeGraph enterGraph)
    {
        var document = new FrontedBehaviorDocument();
        document.ControlBehaviorSets.Add(CreateSet(targetGuid, exitGraph, enterGraph));
        return document;
    }

    private static ControlBehaviorSet CreateSet(
        Guid targetGuid,
        FrontedNodeGraph exitGraph,
        FrontedNodeGraph enterGraph,
        string eventType = "Selection.CharacterPick") =>
        new()
        {
            BehaviorGuid = targetGuid,
            DisplayName = "SurPick0",
            Behaviors =
            [
                new FrontedBehavior
                {
                    Kind = FrontedBehaviorKind.Transition,
                    Enabled = true,
                    TransitionTrigger = new TriggerDescriptor { EventType = eventType },
                    ExitGraph = exitGraph,
                    EnterGraph = enterGraph
                }
            ]
        };

    private static FrontedTransitionRequest CreateRequest(
        Guid targetGuid,
        int playerIndex,
        string transitionType = "Selection.CharacterPick") =>
        new()
        {
            WindowType = "BpWindow",
            TransitionType = transitionType,
            TargetBehaviorGuid = targetGuid,
            TargetDisplayName = $"SurPick{playerIndex}",
            Payload =
            {
                ["Event.Camp"] = "Sur",
                ["Event.PlayerIndex"] = playerIndex
            }
        };
}
