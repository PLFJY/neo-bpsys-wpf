using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests for the Loop behavior lifecycle in <see cref="FrontedBehaviorRuntimeHost" />.
///
/// <see cref="FrontedBehaviorRuntimeHost" /> is internal in neo-bpsys-wpf.Core and the
/// test assembly does not have InternalsVisibleTo for that project, so we use reflection
/// to create the host and invoke its methods.
/// </summary>
public class FrontedBehaviorRuntimeLoopTest
{
    /// <summary>
    /// StartTrigger 发布后，先执行 StartGraph，然后 LoopGraph 开始循环。
    /// </summary>
    [Fact]
    public async Task BehaviorRuntime_Loop_StartTrigger_StartsStartAndLoopGraphs()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime();
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                EndTrigger = new TriggerDescriptor { EventType = "end" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy { RepeatCount = 1 }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            runtime.ExecutionCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });

            await runtime.ExecutionCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // StartGraph executed once, LoopGraph executed once (RepeatCount=1)
            Assert.Contains(behavior.StartGraph, runtime.ExecutedGraphs);
            Assert.Contains(behavior.LoopGraph, runtime.ExecutedGraphs);
            Assert.Equal(2, runtime.ExecutedGraphs.Count);
        });
    }

    /// <summary>
    /// 循环启动后发布 EndTrigger，等待执行 StopGraph。
    /// </summary>
    [Fact]
    public async Task BehaviorRuntime_Loop_EndTrigger_RunsStopGraph()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                // Keep LoopGraph "running" by blocking on a gate
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                EndTrigger = new TriggerDescriptor { EventType = "end" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = false
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            // Publish start trigger — starts StartGraph, then blocks on LoopGraph
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            // Now LoopGraph is blocked; publish end trigger
            var stopGraphCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            runtime.ExecutionCompleted = stopGraphCompleted;
            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            await stopGraphCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Contains(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    /// <summary>
    /// 连续发布两次 StartTrigger，只启动一个循环实例（默认 IgnoreIfRunning）。
    /// </summary>
    [Fact]
    public async Task BehaviorRuntime_Loop_DoesNotStartDuplicateInstance()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                EndTrigger = new TriggerDescriptor { EventType = "end" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    ReentryPolicy = FrontedReentryPolicy.IgnoreIfRunning
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            // First start trigger
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            // Reset tracking to count only what happens after the second trigger
            runtime.ExecutedGraphs.Clear();

            // Second start trigger while running — should be ignored
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await Task.Delay(200); // Let any async processing settle

            // No additional graph executions
            Assert.Empty(runtime.ExecutedGraphs);
        });
    }

    /// <summary>
    /// 使用 InterruptPrevious 策略时，再次发布 StartTrigger 会取消旧的循环并重新启动。
    /// </summary>
    /// <remarks>
    /// 当前 <see cref="FrontedBehaviorRuntimeHost" /> 的 ProcessLoop 实现仅在未运行状态处理 StartTrigger，
    /// 运行中状态仅检查 EndTrigger。InterruptPrevious 支持尚未在 Loop 行为中实现。
    /// 此测试记录了期望行为。
    /// </remarks>
    [Fact]
    public async Task BehaviorRuntime_Loop_InterruptPrevious_Restarts()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                EndTrigger = new TriggerDescriptor { EventType = "end" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    ReentryPolicy = FrontedReentryPolicy.InterruptPrevious,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = false
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            // First start trigger
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            // Clear tracking
            runtime.ExecutedGraphs.Clear();

            // Second start trigger with InterruptPrevious — should cancel old and restart
            runtime.StartGraphExecuted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });

            // Wait for a second StartGraph execution (indicating restart)
            await runtime.StartGraphExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        });
    }

    /// <summary>
    /// ResetOnStop=true 时，停止循环后调用 ResetTarget。
    /// </summary>
    [Fact]
    public async Task BehaviorRuntime_Loop_ResetOnStop_CallsReset()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var animationRuntime = new RecordingAnimationRuntime();
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                EndTrigger = new TriggerDescriptor { EventType = "end" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = true
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime, animationRuntime);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            var stopCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            runtime.ExecutionCompleted = stopCompleted;
            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            await stopCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Contains(behavior.BehaviorId, animationRuntime.ResetTargetCalls);
        });
    }

    /// <summary>
    /// StopMode=StopImmediately 时，收到 EndTrigger 后直接取消 LoopGraph 而不执行 StopGraph。
    /// </summary>
    [Fact]
    public async Task BehaviorRuntime_Loop_StopImmediately_CancelsLoop()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                EndTrigger = new TriggerDescriptor { EventType = "end" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.StopImmediately,
                    ResetOnStop = false
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });
            await Task.Delay(200);

            // StopGraph should NOT be executed
            Assert.DoesNotContain(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    /// <summary>
    /// StopMode=RunStopGraph 时，收到 EndTrigger 后执行 StopGraph。
    /// </summary>
    [Fact]
    public async Task BehaviorRuntime_Loop_RunStopGraph_ExecutesStopGraph()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                EndTrigger = new TriggerDescriptor { EventType = "end" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = false
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            var stopCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            runtime.ExecutionCompleted = stopCompleted;
            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            await stopCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Contains(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    // ---------------------------------------------------------------
    // STA thread helper
    // ---------------------------------------------------------------

    /// <summary>
    /// Runs the given async action on an STA thread, required for WPF control creation.
    /// </summary>
    private static async Task RunOnStaThreadAsync(Func<Task> action)
    {
        ExceptionDispatchInfo? exception = null;
        var tcs = new TaskCompletionSource();
        var thread = new Thread(async () =>
        {
            try
            {
                await action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await tcs.Task;
        exception?.Throw();
    }

    // ---------------------------------------------------------------
    // Reflection helpers for FrontedBehaviorRuntimeHost
    // ---------------------------------------------------------------

    private static readonly Type HostType = typeof(FrontedBehaviorRuntimeContext).Assembly
        .GetType("neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedBehaviorRuntimeHost")!;

    private static IDisposable CreateHost(
        ControlledGraphRuntime graphRuntime,
        RecordingAnimationRuntime? animationRuntime = null)
    {
        var context = new FrontedBehaviorRuntimeContext
        {
            WindowId = "TestWindow",
            WindowType = "BpWindow",
            CanvasName = "TestCanvas",
            RootCanvas = new Canvas(),
            CanvasConfig = new FrontedCanvasConfig(),
            SharedDataService = new MockSharedDataService(),
            Logger = NullLogger.Instance,
            IsDesignerPreview = true
        };

        var eventBus = new MockEventBus();
        var triggerEvaluator = new FrontedBehaviorTriggerEvaluator();
        var animRuntime = animationRuntime ?? new RecordingAnimationRuntime();

        var constructor = HostType.GetConstructor([
            typeof(FrontedBehaviorRuntimeContext),
            typeof(IFrontedEventBus),
            typeof(IFrontedNodeGraphRuntime),
            typeof(IFrontedAnimationRuntime),
            typeof(FrontedBehaviorTriggerEvaluator)
        ])!;

        return (IDisposable)constructor.Invoke([context, eventBus, graphRuntime, animRuntime, triggerEvaluator]);
    }

    private static async Task AttachHost(IDisposable host, FrontedBehaviorDocument document)
    {
        var method = HostType.GetMethod("AttachAsync")!;
        var task = (Task)method.Invoke(host, [document])!;
        await task;
    }

    private static void RunEvent(IDisposable host, FrontedBehaviorEvent behaviorEvent)
    {
        // The host subscribed to the MockEventBus via Subscribe(null, OnEventAsync).
        // We publish through the bus, and the bus calls the handler.
        var eventBusField = HostType.GetField("_eventBus", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var eventBus = (MockEventBus)eventBusField.GetValue(host)!;

        eventBus.Publish(behaviorEvent);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static FrontedBehaviorDocument CreateDocument(FrontedBehavior behavior)
    {
        return new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = "BpWindow",
            CanvasName = "TestCanvas",
            ControlBehaviorSets =
            [
                new ControlBehaviorSet
                {
                    BehaviorGuid = behavior.BehaviorId,
                    DisplayName = "TestControl",
                    Behaviors = [behavior]
                }
            ]
        };
    }

    // ---------------------------------------------------------------
    // Mock types
    // ---------------------------------------------------------------

    private sealed class MockEventBus : IFrontedEventBus
    {
        public event EventHandler<FrontedBehaviorEvent>? EventPublished;
        private Func<FrontedBehaviorEvent, Task>? _handler;

        public void Publish(FrontedBehaviorEvent behaviorEvent)
        {
            var handler = _handler;
            if (handler is not null)
            {
                _ = handler(behaviorEvent);
            }

            EventPublished?.Invoke(this, behaviorEvent);
        }

        public IDisposable Subscribe(string? eventType, Func<FrontedBehaviorEvent, Task> handler)
        {
            if (_handler is not null)
            {
                throw new InvalidOperationException("MockEventBus only supports a single subscription.");
            }

            _handler = handler;
            return new DisposableAction(() => _handler = null);
        }
    }

    private sealed class DisposableAction(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    private sealed class MockSharedDataService : ISharedDataService
    {
        public event EventHandler? CurrentGameChanged;
        public event EventHandler<BanCountChangedEventArgs>? BanCountChanged;
        public event EventHandler? IsTraitVisibleChanged;
        public event EventHandler? IsBo3ModeChanged;
        public event EventHandler? CountDownValueChanged;
        public event EventHandler? TeamSwapped;
        public event EventHandler? IsMapV2BreathingChanged;
        public event EventHandler? IsMapV2CampVisibleChanged;
        public event EventHandler? PickedMapChanged;
        public event EventHandler? MapV2BannedChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public string RemainingSeconds { get; set; } = string.Empty;
        public Team HomeTeam => throw new NotImplementedException();
        public Team AwayTeam => throw new NotImplementedException();
        public Game CurrentGame => throw new NotImplementedException();
        public SortedDictionary<string, Character> SurCharaDict
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
        public SortedDictionary<string, Character> HunCharaDict
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
        public ObservableCollection<bool> CanCurrentSurBannedList => throw new NotImplementedException();
        public ObservableCollection<bool> CanCurrentHunBannedList => throw new NotImplementedException();
        public ObservableCollection<bool> CanGlobalSurBannedList => throw new NotImplementedException();
        public ObservableCollection<bool> CanGlobalHunBannedList => throw new NotImplementedException();
        public bool IsTraitVisible { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsBo3Mode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsMapV2Breathing { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsMapV2CampVisible { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void NewGame() => throw new NotImplementedException();
        public Task ImportGameAsync(string filePath) => throw new NotImplementedException();
        public void SetBanCount(BanListName listName, int count) => throw new NotImplementedException();
        public void TimerStart(int? seconds) => throw new NotImplementedException();
        public void TimerStop() => throw new NotImplementedException();
    }

    /// <summary>
    /// Controlled implementation of <see cref="IFrontedNodeGraphRuntime" /> for loop behavior tests.
    /// Tracks which graphs were executed and supports blocking on LoopGraph for EndTrigger tests.
    /// </summary>
    private sealed class ControlledGraphRuntime : IFrontedNodeGraphRuntime
    {
        /// <summary>Graphs that have been executed, in order.</summary>
        public List<FrontedNodeGraph> ExecutedGraphs { get; } = [];

        /// <summary>
        /// When non-null, execution of any graph will block on this gate.
        /// Used by <see cref="FrontedBehaviorRuntimeHost.ExecuteLoopStartAsync" /> to keep
        /// the LoopGraph "running" so that EndTrigger tests can fire.
        /// </summary>
        public TaskCompletionSource? LoopGate { get; set; }

        /// <summary>
        /// When non-null, signals completion after an execution is recorded.
        /// Set to a new TCS before triggering an event; complete after assertions are done.
        /// </summary>
        public TaskCompletionSource? ExecutionCompleted { get; set; }

        /// <summary>
        /// When non-null, is set when any graph is first executed.
        /// </summary>
        public TaskCompletionSource? StartGraphExecuted { get; set; }

        public async Task<FrontedGraphExecutionResult> ExecuteAsync(
            FrontedNodeGraph graph,
            FrontedGraphExecutionContext context,
            CancellationToken cancellationToken)
        {
            ExecutedGraphs.Add(graph);

            // Signal StartGraph execution
            if (StartGraphExecuted is not null)
            {
                StartGraphExecuted.TrySetResult();
            }

            // Block only the first LoopGraph execution. StartGraph and StopGraph should
            // complete normally so stop-mode assertions do not depend on timeouts.
            if (LoopGate is not null && ExecutedGraphs.Count == 2)
            {
                using var registration = cancellationToken.Register(() =>
                {
                    try { LoopGate.TrySetCanceled(cancellationToken); } catch { }
                });
                try
                {
                    await LoopGate.Task;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Normal cancellation — execution completed signal still fires below
                }
            }

            // Signal completion
            ExecutionCompleted?.TrySetResult();

            if (cancellationToken.IsCancellationRequested)
            {
                return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Cancelled };
            }

            return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success };
        }

        /// <summary>
        /// Waits until a graph has been executed at least once.
        /// </summary>
        public async Task WaitForStartGraphAsync(TimeSpan timeout)
        {
            if (ExecutedGraphs.Count > 0)
                return;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            StartGraphExecuted = tcs;
            await tcs.Task.WaitAsync(timeout);
        }
    }

    /// <summary>
    /// Records <see cref="IFrontedAnimationRuntime.ResetTarget" /> calls.
    /// </summary>
    private sealed class RecordingAnimationRuntime : IFrontedAnimationRuntime
    {
        public List<Guid> ResetTargetCalls { get; } = [];

        public Task ExecuteAsync(
            IReadOnlyList<FrontedGraphActionRequest> actions,
            FrontedAnimationExecutionContext context,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ExecuteAsync(
            FrontedGraphActionRequest action,
            FrontedAnimationExecutionContext context,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public void ResetTarget(Guid behaviorGuid, FrontedAnimationExecutionContext context)
        {
            ResetTargetCalls.Add(behaviorGuid);
        }

        public void ResetAll(FrontedAnimationExecutionContext context) { }

        public void Release(FrameworkElement root) { }
    }
}
