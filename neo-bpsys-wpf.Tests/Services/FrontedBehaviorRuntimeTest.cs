using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests for <see cref="FrontedBehaviorRuntimeHost" />.
///
/// FrontedBehaviorRuntimeHost is internal sealed in neo-bpsys-wpf.Core
/// (InternalsVisibleTo only grants access to neo-bpsys-wpf, not the test project),
/// so these tests use reflection via <see cref="FrontedBehaviorRuntimeHostProxy" />.
/// </summary>
public class FrontedBehaviorRuntimeTest
{
    private static readonly FrontedNodeCatalog Catalog = new();

    // ---------------------------------------------------------------
    // Test 1: ManualTrigger runs the OneShot graph once
    // ---------------------------------------------------------------

    [Fact]
    public async Task BehaviorRuntime_ManualTrigger_RunsOneShotGraph()
    {
        var behaviorGuid = Guid.NewGuid();
        var graph = new FrontedNodeGraph();

        var document = CreateDocument(
            behaviorGuid,
            eventType: "ManualTrigger",
            graph: graph,
            reentryPolicy: FrontedReentryPolicy.InterruptPrevious);

        await RunOnStaThreadAsync(async () =>
        {
            var proxy = CreateHostWithMocks(document, out var eventHandler, out var graphRuntimeMock);

            using (proxy)
            {
                await proxy.AttachAsync(document);
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await Task.Delay(100);
            }

            graphRuntimeMock.Verify(
                x => x.ExecuteAsync(
                    It.IsAny<FrontedNodeGraph>(),
                    It.Is<FrontedGraphExecutionContext>(ctx => ctx.BehaviorGuid != Guid.Empty),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        });
    }

    // ---------------------------------------------------------------
    // Test 2: InterruptPrevious cancels the old run
    // ---------------------------------------------------------------

    [Fact]
    public async Task BehaviorRuntime_OneShot_InterruptPrevious_CancelsOldRun()
    {
        var behaviorGuid = Guid.NewGuid();
        var graph = new FrontedNodeGraph();

        var document = CreateDocument(
            behaviorGuid,
            eventType: "ManualTrigger",
            graph: graph,
            reentryPolicy: FrontedReentryPolicy.InterruptPrevious);

        await RunOnStaThreadAsync(async () =>
        {
            var proxy = CreateHostWithBlockingMocks(document, out var eventHandler, out var graphRuntimeMock);

            using (proxy)
            {
                await proxy.AttachAsync(document);

                // First trigger
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await graphRuntimeMock.FirstCallStarted;

                // Second trigger — should cancel the first
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await Task.Delay(100);
            }

            Assert.Equal(2, graphRuntimeMock.CallCount);
            Assert.True(graphRuntimeMock.FirstTokenCancelled);
        });
    }

    // ---------------------------------------------------------------
    // Test 3: IgnoreIfRunning skips the second trigger
    // ---------------------------------------------------------------

    [Fact]
    public async Task BehaviorRuntime_OneShot_IgnoreIfRunning_SkipsSecondRun()
    {
        var behaviorGuid = Guid.NewGuid();
        var graph = new FrontedNodeGraph();

        var document = CreateDocument(
            behaviorGuid,
            eventType: "ManualTrigger",
            graph: graph,
            reentryPolicy: FrontedReentryPolicy.IgnoreIfRunning);

        await RunOnStaThreadAsync(async () =>
        {
            var proxy = CreateHostWithBlockingMocks(document, out var eventHandler, out var graphRuntimeMock);

            using (proxy)
            {
                await proxy.AttachAsync(document);

                // First trigger
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await graphRuntimeMock.FirstCallStarted;

                // Second trigger — should be ignored
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await Task.Delay(100);
            }

            // Only one execution — the second should have been skipped
            Assert.Equal(1, graphRuntimeMock.CallCount);
        });
    }

    // ---------------------------------------------------------------
    // Test 4: Graph with non-existent target does not throw
    // ---------------------------------------------------------------

    [Fact]
    public async Task BehaviorRuntime_MissingTarget_SkipsWithoutThrow()
    {
        var behaviorGuid = Guid.NewGuid();
        var graph = CreateGraphWithMissingTarget();

        var document = CreateDocument(
            behaviorGuid,
            eventType: "ManualTrigger",
            graph: graph,
            reentryPolicy: FrontedReentryPolicy.InterruptPrevious);

        await RunOnStaThreadAsync(async () =>
        {
            // Use a real graph runtime so the graph actually gets processed
            var proxy = CreateHostWithRealGraphRuntime(document, out var eventHandler);

            using (proxy)
            {
                await proxy.AttachAsync(document);

                // This should not throw despite the graph referencing a non-existent target
                var ex = await Record.ExceptionAsync(() =>
                    eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" }));
                Assert.Null(ex);

                await Task.Delay(100);
            }
        });
    }

    // ---------------------------------------------------------------
    // Helper: document construction
    // ---------------------------------------------------------------

    private static FrontedBehaviorDocument CreateDocument(
        Guid behaviorGuid,
        string eventType,
        FrontedNodeGraph graph,
        FrontedReentryPolicy reentryPolicy)
    {
        return new FrontedBehaviorDocument
        {
            ControlBehaviorSets =
            [
                new ControlBehaviorSet
                {
                    BehaviorGuid = behaviorGuid,
                    DisplayName = "TestControl",
                    Behaviors =
                    [
                        new FrontedBehavior
                        {
                            Kind = FrontedBehaviorKind.OneShot,
                            Enabled = true,
                            Trigger = new TriggerDescriptor { EventType = eventType },
                            ReentryPolicy = reentryPolicy,
                            Graph = graph
                        }
                    ]
                }
            ]
        };
    }

    private static FrontedBehaviorRuntimeContext CreateContext()
    {
        return new FrontedBehaviorRuntimeContext
        {
            WindowId = "TestWindow",
            WindowType = "TestType",
            CanvasName = "BaseCanvas",
            RootCanvas = new Canvas(),
            WindowConfig = neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(new FrontedCanvasConfig()),
            SharedDataService = Mock.Of<ISharedDataService>()
        };
    }

    /// <summary>
    /// Builds a minimal graph start -> action.setProperty("Target" = "NonExistent") -> end
    /// to verify the runtime doesn't throw for missing targets.
    /// </summary>
    private static FrontedNodeGraph CreateGraphWithMissingTarget()
    {
        var start = Catalog.CreateNode("flow.start");
        var setProp = Catalog.CreateNode("action.setProperty");
        setProp.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        setProp.Properties["Target"] = JsonSerializer.SerializeToElement("NonExistent");
        var end = Catalog.CreateNode("flow.end");

        return new FrontedNodeGraph
        {
            Nodes = [start, setProp, end],
            Connections =
            [
                Link(start, "Out", setProp, "In"),
                Link(setProp, "Out", end, "In")
            ]
        };
    }

    private static FrontedNodeConnection Link(FrontedNode source, string sourcePort, FrontedNode target, string targetPort) =>
        new() { SourceNodeId = source.NodeId, SourcePort = sourcePort, TargetNodeId = target.NodeId, TargetPort = targetPort };

    // ---------------------------------------------------------------
    // Host creation helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a host with mocked graph runtime. Returns a proxy, the captured event handler,
    /// and the mock so the caller can verify calls.
    /// </summary>
    private static FrontedBehaviorRuntimeHostProxy CreateHostWithMocks(
        FrontedBehaviorDocument document,
        out Func<FrontedBehaviorEvent, Task> eventHandler,
        out Mock<IFrontedNodeGraphRuntime> graphRuntimeMock)
    {
        var context = CreateContext();

        // Event bus mock: capture the subscription handler
        var eventBusMock = new Mock<IFrontedEventBus>();
        Func<FrontedBehaviorEvent, Task>? capturedHandler = null;
        eventBusMock
            .Setup(x => x.Subscribe(It.IsAny<string?>(), It.IsAny<Func<FrontedBehaviorEvent, Task>>()))
            .Callback<string?, Func<FrontedBehaviorEvent, Task>>((_, handler) => capturedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        // Graph runtime mock
        graphRuntimeMock = new Mock<IFrontedNodeGraphRuntime>();
        graphRuntimeMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<FrontedNodeGraph>(),
                It.IsAny<FrontedGraphExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success });

        var animationRuntimeMock = new Mock<IFrontedAnimationRuntime>();
        var triggerEvaluator = new FrontedBehaviorTriggerEvaluator();

        var proxy = new FrontedBehaviorRuntimeHostProxy(context, eventBusMock.Object, graphRuntimeMock.Object,
            animationRuntimeMock.Object, triggerEvaluator);

        eventHandler = e => capturedHandler?.Invoke(e) ?? Task.CompletedTask;
        return proxy;
    }

    /// <summary>
    /// Creates a host with a <see cref="BlockableGraphRuntimeMock" /> that lets the test
    /// control when the first execution completes.
    /// </summary>
    private static FrontedBehaviorRuntimeHostProxy CreateHostWithBlockingMocks(
        FrontedBehaviorDocument document,
        out Func<FrontedBehaviorEvent, Task> eventHandler,
        out BlockableGraphRuntimeMock graphRuntimeMock)
    {
        var context = CreateContext();

        var eventBusMock = new Mock<IFrontedEventBus>();
        Func<FrontedBehaviorEvent, Task>? capturedHandler = null;
        eventBusMock
            .Setup(x => x.Subscribe(It.IsAny<string?>(), It.IsAny<Func<FrontedBehaviorEvent, Task>>()))
            .Callback<string?, Func<FrontedBehaviorEvent, Task>>((_, handler) => capturedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        graphRuntimeMock = new BlockableGraphRuntimeMock();

        var animationRuntimeMock = new Mock<IFrontedAnimationRuntime>();
        var triggerEvaluator = new FrontedBehaviorTriggerEvaluator();

        var proxy = new FrontedBehaviorRuntimeHostProxy(context, eventBusMock.Object, graphRuntimeMock,
            animationRuntimeMock.Object, triggerEvaluator);

        eventHandler = e => capturedHandler?.Invoke(e) ?? Task.CompletedTask;
        return proxy;
    }

    /// <summary>
    /// Creates a host with a real <see cref="FrontedNodeGraphRuntime" /> so the graph
    /// actually gets processed (used for the MissingTarget test).
    /// </summary>
    private static FrontedBehaviorRuntimeHostProxy CreateHostWithRealGraphRuntime(
        FrontedBehaviorDocument document,
        out Func<FrontedBehaviorEvent, Task> eventHandler)
    {
        var context = CreateContext();

        var eventBusMock = new Mock<IFrontedEventBus>();
        Func<FrontedBehaviorEvent, Task>? capturedHandler = null;
        eventBusMock
            .Setup(x => x.Subscribe(It.IsAny<string?>(), It.IsAny<Func<FrontedBehaviorEvent, Task>>()))
            .Callback<string?, Func<FrontedBehaviorEvent, Task>>((_, handler) => capturedHandler = handler)
            .Returns(Mock.Of<IDisposable>());

        var graphRuntime = new FrontedNodeGraphRuntime(
            new FrontedNodeCatalog(),
            new FrontedNodeGraphValidator(new FrontedNodeCatalog()));

        var animationRuntimeMock = new Mock<IFrontedAnimationRuntime>();
        var triggerEvaluator = new FrontedBehaviorTriggerEvaluator();

        var proxy = new FrontedBehaviorRuntimeHostProxy(context, eventBusMock.Object, graphRuntime,
            animationRuntimeMock.Object, triggerEvaluator);

        eventHandler = e => capturedHandler?.Invoke(e) ?? Task.CompletedTask;
        return proxy;
    }

    /// <summary>
    /// Runs the given async action on an STA thread, required for WPF control creation.
    /// </summary>
    private static async Task RunOnStaThreadAsync(Func<Task> action)
    {
        await WpfTestThread.RunAsync(action);
    }

    // ---------------------------------------------------------------
    // Reflection proxy for internal FrontedBehaviorRuntimeHost
    // ---------------------------------------------------------------

    /// <summary>
    /// Proxy that uses reflection to access the internal <c>FrontedBehaviorRuntimeHost</c>.
    /// </summary>
    internal sealed class FrontedBehaviorRuntimeHostProxy : IDisposable
    {
        private static readonly Type HostType = typeof(FrontedNodeGraphRuntime).Assembly
            .GetType("neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedBehaviorRuntimeHost")
            ?? throw new InvalidOperationException("Type FrontedBehaviorRuntimeHost not found.");

        private static readonly ConstructorInfo Constructor =
            HostType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();

        private static readonly MethodInfo AttachAsyncMethod =
            HostType.GetMethod("AttachAsync", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Method AttachAsync not found.");

        private readonly object _instance;

        /// <summary>
        /// Initializes a new instance via the reflection proxy.
        /// </summary>
        public FrontedBehaviorRuntimeHostProxy(
            FrontedBehaviorRuntimeContext context,
            IFrontedEventBus eventBus,
            IFrontedNodeGraphRuntime graphRuntime,
            IFrontedAnimationRuntime animationRuntime,
            FrontedBehaviorTriggerEvaluator triggerEvaluator)
        {
            _instance = Constructor.Invoke([context, eventBus, graphRuntime, animationRuntime, triggerEvaluator]);
        }

        /// <summary>
        /// Calls <c>AttachAsync</c> on the internal host.
        /// </summary>
        public Task AttachAsync(FrontedBehaviorDocument document)
        {
            return (Task)AttachAsyncMethod.Invoke(_instance, [document])!;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ((IDisposable)_instance).Dispose();
        }
    }

    // ---------------------------------------------------------------
    // Blockable graph runtime mock
    // ---------------------------------------------------------------

    /// <summary>
    /// Graph runtime that blocks on the first call until the test signals,
    /// allowing concurrency tests (InterruptPrevious / IgnoreIfRunning).
    /// </summary>
    internal sealed class BlockableGraphRuntimeMock : IFrontedNodeGraphRuntime
    {
        private int _callCount;

        /// <summary>Number of times <see cref="ExecuteAsync" /> has been called.</summary>
        public int CallCount => _callCount;

        /// <summary>Signalled when the first call has entered the method body.</summary>
        public Task FirstCallStarted => FirstCallStartedTcs.Task;

        internal TaskCompletionSource FirstCallStartedTcs { get; } = new();
        internal TaskCompletionSource FirstCallBlockedTcs { get; } = new();
        internal CancellationToken? FirstToken { get; private set; }

        /// <summary>Whether the token from the first call was cancelled.</summary>
        public bool FirstTokenCancelled => FirstToken is { IsCancellationRequested: true };

        /// <inheritdoc />
        public async Task<FrontedGraphExecutionResult> ExecuteAsync(
            FrontedNodeGraph graph,
            FrontedGraphExecutionContext context,
            CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _callCount);

            if (count == 1)
            {
                FirstToken = cancellationToken;
                FirstCallStartedTcs.TrySetResult();

                // Block until cancelled
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when InterruptPrevious cancels the old run
                }

                FirstCallBlockedTcs.TrySetResult();
                return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Cancelled };
            }

            // Second call completes immediately
            return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success };
        }
    }
}
