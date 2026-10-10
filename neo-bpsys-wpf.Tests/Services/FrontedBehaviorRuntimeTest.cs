using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 针对 <see cref="FrontedBehaviorRuntimeHost" /> 的测试。
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
            var host = CreateHostWithMocks(document, out var eventHandler, out var graphRuntimeMock);

            using (host)
            {
                await host.AttachAsync(document);
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
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
            var host = CreateHostWithBlockingMocks(document, out var eventHandler, out var graphRuntimeMock);

            using (host)
            {
                await host.AttachAsync(document);

                // First trigger
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await graphRuntimeMock.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // Second trigger — should cancel the first
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await graphRuntimeMock.FirstCallBlockedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await graphRuntimeMock.SecondCallStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
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
            var host = CreateHostWithBlockingMocks(document, out var eventHandler, out var graphRuntimeMock);

            using (host)
            {
                await host.AttachAsync(document);

                // First trigger
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
                await graphRuntimeMock.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                // Second trigger — should be ignored
                await eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" });
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
            var host = CreateHostWithRealGraphRuntime(document, out var eventHandler);

            using (host)
            {
                await host.AttachAsync(document);

                // This should not throw despite the graph referencing a non-existent target
                var ex = await Record.ExceptionAsync(() =>
                    eventHandler(new FrontedBehaviorEvent { EventType = "ManualTrigger" }));
                Assert.Null(ex);

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
    /// 构建一个最小图 start -> action.setProperty("Target" = "NonExistent") -> end，
    /// 用于验证运行时在目标不存在时不会抛出异常。
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
    /// 创建一个使用模拟图运行时的宿主。返回宿主、捕获的事件处理器，
    /// 以及 mock，调用方可以据此验证调用情况。
    /// </summary>
    private static FrontedBehaviorRuntimeHost CreateHostWithMocks(
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

        var host = new FrontedBehaviorRuntimeHost(context, eventBusMock.Object, graphRuntimeMock.Object,
            animationRuntimeMock.Object, triggerEvaluator);

        eventHandler = e => capturedHandler?.Invoke(e) ?? Task.CompletedTask;
        return host;
    }

    /// <summary>
    /// 创建一个使用 <see cref="BlockableGraphRuntimeMock" /> 的宿主，让测试可以
    /// 控制首次执行何时完成。
    /// </summary>
    private static FrontedBehaviorRuntimeHost CreateHostWithBlockingMocks(
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

        var host = new FrontedBehaviorRuntimeHost(context, eventBusMock.Object, graphRuntimeMock,
            animationRuntimeMock.Object, triggerEvaluator);

        eventHandler = e => capturedHandler?.Invoke(e) ?? Task.CompletedTask;
        return host;
    }

    /// <summary>
    /// 创建一个使用真实 <see cref="FrontedNodeGraphRuntime" /> 的宿主，使图能被实际处理
    /// （用于 MissingTarget 测试）。
    /// </summary>
    private static FrontedBehaviorRuntimeHost CreateHostWithRealGraphRuntime(
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

        var host = new FrontedBehaviorRuntimeHost(context, eventBusMock.Object, graphRuntime,
            animationRuntimeMock.Object, triggerEvaluator);

        eventHandler = e => capturedHandler?.Invoke(e) ?? Task.CompletedTask;
        return host;
    }

    /// <summary>
    /// 在 STA 线程上运行给定的异步操作，WPF 控件创建需要这样做。
    /// </summary>
    private static async Task RunOnStaThreadAsync(Func<Task> action)
    {
        await WpfTestThread.RunAsync(action);
    }

    // ---------------------------------------------------------------
    // Blockable graph runtime mock
    // ---------------------------------------------------------------

    /// <summary>
    /// 图运行时，在测试发出信号前会阻塞首次调用，
    /// 用于支持并发测试（InterruptPrevious / IgnoreIfRunning）。
    /// </summary>
    internal sealed class BlockableGraphRuntimeMock : IFrontedNodeGraphRuntime
    {
        private int _callCount;

        /// <summary><see cref="ExecuteAsync" /> 被调用的次数。</summary>
        public int CallCount => _callCount;

        /// <summary>首次调用进入方法体时发出的信号。</summary>
        public Task FirstCallStarted => FirstCallStartedTcs.Task;

        internal TaskCompletionSource FirstCallStartedTcs { get; } = new();
        internal TaskCompletionSource FirstCallBlockedTcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondCallStartedTcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken? FirstToken { get; private set; }

        /// <summary>首次调用的令牌是否已被取消。</summary>
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

            SecondCallStartedTcs.TrySetResult();

            // Second call completes immediately
            return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success };
        }
    }
}
