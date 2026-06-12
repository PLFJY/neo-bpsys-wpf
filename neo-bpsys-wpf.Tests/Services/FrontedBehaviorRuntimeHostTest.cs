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

public class FrontedBehaviorRuntimeHostTest
{
    [Fact]
    public async Task RuntimeHost_Attach_PublishesCanvasLoaded()
    {
        await RunOnStaThreadAsync(async () =>
        {
            // Arrange
            var canvas = new Canvas();
            var context = CreateContext(canvas);
            var document = CreateEmptyDocument();

            var behaviorService = new Mock<IFrontedBehaviorService>();
            behaviorService
                .Setup(s => s.LoadDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            var eventBus = new FrontedEventBus();
            var canvasLoadedEvents = new List<FrontedBehaviorEvent>();
            eventBus.EventPublished += (_, e) =>
            {
                if (e.EventType == "CanvasLoaded")
                    canvasLoadedEvents.Add(e);
            };

            var manager = CreateManager(behaviorService.Object, eventBus);

            // Act
            await manager.AttachHostAsync(context);

            // Assert
            var loaded = Assert.Single(canvasLoadedEvents);
            Assert.Equal("CanvasLoaded", loaded.EventType);
            Assert.Equal("TestWindow", loaded.WindowId);
            Assert.Equal("BpWindow", loaded.WindowType);
            Assert.Equal("BaseCanvas", loaded.CanvasName);
        });
    }

    [Fact]
    public async Task RuntimeHost_Detach_CancelsRunningBehaviors()
    {
        await RunOnStaThreadAsync(async () =>
        {
            // Arrange
            var canvas = new Canvas();
            var context = CreateContext(canvas);
            var behaviorId = Guid.NewGuid();
            var setGuid = Guid.NewGuid();
            var document = CreateDocumentWithOneShot(behaviorId, setGuid, "TestEvent");

            var behaviorService = new Mock<IFrontedBehaviorService>();
            behaviorService
                .Setup(s => s.LoadDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            var eventBus = new FrontedEventBus();

            var capturedCancellationToken = CancellationToken.None;
            var executionTcs = new TaskCompletionSource<FrontedGraphExecutionResult>();
            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            graphRuntime
                .Setup(r => r.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FrontedNodeGraph, FrontedGraphExecutionContext, CancellationToken>((_, _, ct) =>
                {
                    capturedCancellationToken = ct;
                    ct.Register(() => executionTcs.TrySetCanceled(ct));
                })
                .Returns(executionTcs.Task);

            var manager = CreateManager(behaviorService.Object, eventBus, graphRuntime: graphRuntime.Object);

            // Act
            await manager.AttachHostAsync(context);

            // Trigger the behavior by publishing a matching event
            eventBus.Publish(new FrontedBehaviorEvent
            {
                EventType = "TestEvent",
                WindowId = "TestWindow",
                CanvasName = "BaseCanvas"
            });

            // Detach — should cancel all running behaviors
            manager.DetachHost("TestWindow");

            // Assert
            Assert.True(capturedCancellationToken.IsCancellationRequested);
        });
    }

    [Fact]
    public async Task RuntimeHost_Detach_ReleasesAnimationRuntimeSession()
    {
        await RunOnStaThreadAsync(async () =>
        {
            // Arrange
            var canvas = new Canvas();
            var context = CreateContext(canvas);
            var document = CreateEmptyDocument();

            var behaviorService = new Mock<IFrontedBehaviorService>();
            behaviorService
                .Setup(s => s.LoadDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            var eventBus = new Mock<IFrontedEventBus>();
            eventBus
                .Setup(b => b.Subscribe(It.IsAny<string?>(), It.IsAny<Func<FrontedBehaviorEvent, Task>>()))
                .Returns(Mock.Of<IDisposable>());

            var animationRuntime = new Mock<IFrontedAnimationRuntime>();

            var manager = CreateManager(
                behaviorService.Object,
                eventBus.Object,
                animationRuntime: animationRuntime.Object);

            // Act
            await manager.AttachHostAsync(context);
            manager.DetachHost("TestWindow");

            // Assert
            animationRuntime.Verify(r => r.Release(canvas), Times.Once);
        });
    }

    [Fact]
    public async Task ReRender_DoesNotDuplicateSubscriptions()
    {
        await RunOnStaThreadAsync(async () =>
        {
            // Arrange
            var canvas = new Canvas();
            var context = CreateContext(canvas);
            var behaviorId = Guid.NewGuid();
            var setGuid = Guid.NewGuid();
            var document = CreateDocumentWithOneShot(behaviorId, setGuid, "TestEvent");

            var behaviorService = new Mock<IFrontedBehaviorService>();
            behaviorService
                .Setup(s => s.LoadDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            var eventBus = new FrontedEventBus();

            var executeCount = 0;
            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            graphRuntime
                .Setup(r => r.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => executeCount++)
                .ReturnsAsync(new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success });

            var manager = CreateManager(behaviorService.Object, eventBus, graphRuntime: graphRuntime.Object);

            // Act — attach, detach, re-attach
            await manager.AttachHostAsync(context);
            manager.DetachHost("TestWindow");
            await manager.AttachHostAsync(context);

            // Publish event — should only invoke ExecuteAsync once
            eventBus.Publish(new FrontedBehaviorEvent
            {
                EventType = "TestEvent",
                WindowId = "TestWindow",
                CanvasName = "BaseCanvas"
            });

            // Assert
            Assert.Equal(1, executeCount);
        });
    }

    [Fact]
    public async Task RuntimeHost_ScopeFiltersWindowAndCanvas()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var canvas = new Canvas();
            var context = CreateContext(canvas);
            var behaviorId = Guid.NewGuid();
            var setGuid = Guid.NewGuid();
            var document = CreateDocumentWithOneShot(behaviorId, setGuid, "ScopedEvent");

            var behaviorService = new Mock<IFrontedBehaviorService>();
            behaviorService
                .Setup(s => s.LoadDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            var eventBus = new FrontedEventBus();
            var executeCount = 0;
            var graphRuntime = new Mock<IFrontedNodeGraphRuntime>();
            graphRuntime
                .Setup(r => r.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.IsAny<FrontedGraphExecutionContext>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => executeCount++)
                .ReturnsAsync(new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success });

            var manager = CreateManager(behaviorService.Object, eventBus, graphRuntime: graphRuntime.Object);
            await manager.AttachHostAsync(context);

            eventBus.Publish(new FrontedBehaviorEvent
            {
                EventType = "ScopedEvent",
                WindowId = "OtherWindow",
                CanvasName = "BaseCanvas"
            });
            eventBus.Publish(new FrontedBehaviorEvent
            {
                EventType = "ScopedEvent",
                WindowId = "TestWindow",
                CanvasName = "OtherCanvas"
            });
            eventBus.Publish(new FrontedBehaviorEvent { EventType = "ScopedEvent" });

            Assert.Equal(1, executeCount);
        });
    }

    private static FrontedBehaviorRuntimeContext CreateContext(Canvas canvas, FrontedCanvasConfig? config = null) => new()
    {
        WindowId = "TestWindow",
        WindowType = "BpWindow",
        CanvasName = "BaseCanvas",
        RootCanvas = canvas,
        WindowConfig = FrontedWindowConfig.FromCanvasConfig(config ?? new FrontedCanvasConfig()),
        SharedDataService = Mock.Of<ISharedDataService>(),
        IsDesignerPreview = false
    };

    private static FrontedBehaviorDocument CreateEmptyDocument() => new()
    {
        Version = 1,
        WindowType = "BpWindow",
        CanvasName = "BaseCanvas",
        ControlBehaviorSets = []
    };

    private static FrontedBehaviorDocument CreateDocumentWithOneShot(
        Guid behaviorId, Guid setGuid, string eventType)
    {
        var document = new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = "BpWindow",
            CanvasName = "BaseCanvas"
        };
        document.ControlBehaviorSets.Add(new ControlBehaviorSet
        {
            BehaviorGuid = setGuid,
            DisplayName = "TestSet",
            Behaviors =
            [
                new FrontedBehavior
                {
                    BehaviorId = behaviorId,
                    Name = "TestBehavior",
                    Kind = FrontedBehaviorKind.OneShot,
                    Enabled = true,
                    Trigger = new TriggerDescriptor { EventType = eventType },
                    Graph = new FrontedNodeGraph()
                }
            ]
        });
        return document;
    }

    private static FrontedBehaviorRuntimeHostManager CreateManager(
        IFrontedBehaviorService? behaviorService = null,
        IFrontedEventBus? eventBus = null,
        IFrontedNodeGraphRuntime? graphRuntime = null,
        IFrontedAnimationRuntime? animationRuntime = null)
    {
        return new FrontedBehaviorRuntimeHostManager(
            behaviorService ?? Mock.Of<IFrontedBehaviorService>(),
            eventBus ?? Mock.Of<IFrontedEventBus>(),
            graphRuntime ?? Mock.Of<IFrontedNodeGraphRuntime>(),
            animationRuntime ?? Mock.Of<IFrontedAnimationRuntime>(),
            new FrontedBehaviorTriggerEvaluator(),
            Mock.Of<ILogger<FrontedBehaviorRuntimeHostManager>>());
    }

    /// <summary>
    /// Runs the given async action on an STA thread, required for WPF control creation.
    /// </summary>
    private static async Task RunOnStaThreadAsync(Func<Task> action)
    {
        await WpfTestThread.RunAsync(action);
    }
}
