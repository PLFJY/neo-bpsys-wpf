using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>覆盖循环生命周期、停止策略和事件上下文的可观察行为。</summary>
public class FrontedBehaviorRuntimeLoopTest
{
    /// <summary>
    /// StartGraph 失败时不应继续执行无限 LoopGraph，以免同步失败造成 Dispatcher 忙等。
    /// </summary>
    [Fact]
    public async Task BehaviorRuntime_Loop_FailedStartGraph_DoesNotRunLoopGraph()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime { FirstExecutionStatus = FrontedGraphExecutionStatus.Failed };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy { RepeatCount = -1 }
            };

            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(behavior));
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });

            await DrainDispatcherAsync();
            Assert.Equal(0, await StopAllLoopsAsync(host, FrontedBehaviorStopReason.ManualClear, TimeSpan.FromSeconds(1)));
            Assert.Single(runtime.ExecutedGraphs);
            Assert.Same(behavior.StartGraph, runtime.ExecutedGraphs[0]);
        });
    }

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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
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
            Assert.Equal(new[] { behavior.StartGraph, behavior.LoopGraph }, runtime.ExecutedGraphs);
        });
    }

    [Fact]
    public async Task BehaviorRuntime_Loop_AnyStopTriggerStopsLoop()
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
                StopTriggers =
                [
                    new TriggerDescriptor
                    {
                        EventType = "Guidance.StepChanged",
                        Filters =
                        [
                            new TriggerFilter
                            {
                                Left = "Event.PreviousAction",
                                Operator = TriggerFilterOperator.Equals,
                                Right = "PickHun"
                            }
                        ]
                    },
                    new TriggerDescriptor { EventType = "Guidance.Cancelled" }
                ],
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

            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(behavior));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "Guidance.Cancelled" });

            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));
            Assert.Contains(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    [Fact]
    public async Task BehaviorRuntime_Loop_PropagatesStartAndStopEventContexts()
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
                StartTrigger = new TriggerDescriptor { EventType = "Guidance.StepChanged" },
                StopTriggers = [new TriggerDescriptor { EventType = "Guidance.Cancelled" }],
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

            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(behavior));

            RunEvent(host, new FrontedBehaviorEvent
            {
                EventType = "Guidance.StepChanged",
                Payload = new Dictionary<string, object?> { ["Action"] = "PickHun" }
            });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));
            RunEvent(host, new FrontedBehaviorEvent
            {
                EventType = "Guidance.Cancelled",
                Payload = new Dictionary<string, object?> { ["Reason"] = "operator" }
            });
            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));

            var startContext = runtime.Executions.Single(item => ReferenceEquals(item.Graph, behavior.StartGraph)).Context;
            var loopContext = runtime.Executions.Single(item => ReferenceEquals(item.Graph, behavior.LoopGraph)).Context;
            var stopContext = runtime.Executions.Single(item => ReferenceEquals(item.Graph, behavior.StopGraph)).Context;
            Assert.Equal("Guidance.StepChanged", startContext.TriggerEventType);
            Assert.Equal("PickHun", startContext.EventPayload["Action"]);
            Assert.Equal("PickHun", loopContext.EventPayload["Action"]);
            Assert.Equal("Guidance.Cancelled", stopContext.TriggerEventType);
            Assert.Equal("operator", stopContext.EventPayload["Reason"]);
            Assert.Equal("PickHun", stopContext.StartEventPayload["Action"]);
            Assert.Equal("operator", stopContext.StopEventPayload["Reason"]);
        });
    }

    [Fact]
    public async Task BehaviorRuntime_Loop_StopTriggerFiltersRemainAnd()
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
                StopTriggers =
                [
                    new TriggerDescriptor
                    {
                        EventType = "Guidance.StepChanged",
                        Filters =
                        [
                            new TriggerFilter
                            {
                                Left = "Event.PreviousAction",
                                Operator = TriggerFilterOperator.Equals,
                                Right = "PickHun"
                            },
                            new TriggerFilter
                            {
                                Left = "Event.PreviousIndexesText",
                                Operator = TriggerFilterOperator.Contains,
                                Right = "0"
                            }
                        ]
                    }
                ],
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

            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(behavior));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent
            {
                EventType = "Guidance.StepChanged",
                Payload = new Dictionary<string, object?>
                {
                    ["PreviousAction"] = "PickHun",
                    ["PreviousIndexesText"] = "[1]"
                }
            });
            await DrainDispatcherAsync();
            Assert.DoesNotContain(behavior.StopGraph, runtime.ExecutedGraphs);

            RunEvent(host, new FrontedBehaviorEvent
            {
                EventType = "Guidance.StepChanged",
                Payload = new Dictionary<string, object?>
                {
                    ["PreviousAction"] = "PickHun",
                    ["PreviousIndexesText"] = "[0]"
                }
            });

            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));
            Assert.Contains(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    [Fact]
    public async Task BehaviorRuntime_StopAllLoopBehaviors_RunsStopGraphAndClearsRegistry()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime();
            var first = LoopBehavior("start1");
            var second = LoopBehavior("start2");

            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(first, second));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start1" });
            await WaitForGraphAsync(runtime, first.LoopGraph, TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start2" });
            await WaitForGraphAsync(runtime, second.LoopGraph, TimeSpan.FromSeconds(5));

            var stopped = await StopAllLoopsAsync(host, FrontedBehaviorStopReason.ManualClear, TimeSpan.FromMilliseconds(1500));

            Assert.Equal(2, stopped);
            Assert.Contains(first.StopGraph, runtime.ExecutedGraphs);
            Assert.Contains(second.StopGraph, runtime.ExecutedGraphs);
            Assert.Equal(0, await StopAllLoopsAsync(host, FrontedBehaviorStopReason.ManualClear, TimeSpan.FromSeconds(1)));
        });
    }

    [Fact]
    public async Task BehaviorRuntime_StopAllLoopBehaviors_TimesOutAndForceClears()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new BlockingStopGraphRuntime();
            var behavior = LoopBehavior("start");

            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(behavior));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.LoopStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var stopped = await StopAllLoopsAsync(host, FrontedBehaviorStopReason.ManualClear, TimeSpan.FromMilliseconds(100));

            Assert.Equal(1, stopped);
            Assert.True(runtime.StopGraphStarted.Task.IsCompleted);
            Assert.Equal(0, await StopAllLoopsAsync(host, FrontedBehaviorStopReason.ManualClear, TimeSpan.FromSeconds(1)));
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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
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
            await DrainDispatcherAsync();

            // No additional graph executions
            Assert.Empty(runtime.ExecutedGraphs);
        });
    }

    /// <summary>
    /// 使用 InterruptPrevious 策略时，再次发布 StartTrigger 会取消旧的循环并重新启动。
    /// </summary>
    /// <remarks>
    /// 当前 <see cref="FrontedBehaviorRuntimeHost" /> 的 ProcessLoop 实现仅在未运行状态处理 StartTrigger，
    /// 运行中状态仅检查 StopTrigger。InterruptPrevious 支持尚未在 Loop 行为中实现。
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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
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

    [Fact]
    public async Task BehaviorRuntime_Loop_StopTriggerRunsBeforeReentryPolicy()
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
                StartTrigger = new TriggerDescriptor
                {
                    EventType = "Guidance.StepChanged",
                    Filters =
                    [
                        new TriggerFilter
                        {
                            Left = "Event.Action",
                            Operator = TriggerFilterOperator.Equals,
                            Right = "PickSur"
                        }
                    ]
                },
                StopTriggers =
                [
                    new TriggerDescriptor
                    {
                        EventType = "Guidance.StepChanged",
                        Filters =
                        [
                            new TriggerFilter
                            {
                                Left = "Event.PreviousAction",
                                Operator = TriggerFilterOperator.Equals,
                                Right = "PickSur"
                            }
                        ]
                    }
                ],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    ReentryPolicy = FrontedReentryPolicy.IgnoreIfRunning,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = false
                }
            };

            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(behavior));

            RunEvent(host, new FrontedBehaviorEvent
            {
                EventType = "Guidance.StepChanged",
                Payload = new Dictionary<string, object?> { ["Action"] = GameAction.PickSur }
            });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent
            {
                EventType = "Guidance.StepChanged",
                Payload = new Dictionary<string, object?>
                {
                    ["Action"] = GameAction.PickSur,
                    ["PreviousAction"] = GameAction.PickSur
                }
            });

            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));
            Assert.Contains(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    /// <summary>
    /// ResetOnStop=true 但 StopMode=RunStopGraph 时，StopGraph 执行后不调用 ResetTarget。
    /// StopGraph 本身就是结束动画，Reset 会覆盖其视觉效果。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BehaviorRuntime_Loop_RunStopGraph_ExecutesWithoutReset(bool resetOnStop)
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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = resetOnStop
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime, animationRuntime);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));

            // StopGraph executed → SuppressReset = true → ResetTarget must NOT be called
            Assert.Contains(behavior.StopGraph, runtime.ExecutedGraphs);
            await DrainDispatcherAsync();
            Assert.Empty(animationRuntime.ResetTargetCalls);
        });
    }

    /// <summary>
    /// StopMode=StopImmediately 时，收到 StopTrigger 后直接取消 LoopGraph 而不执行 StopGraph。
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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
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
            await DrainDispatcherAsync();

            // StopGraph should NOT be executed
            Assert.DoesNotContain(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    [Fact]
    public async Task Loop_EachLoopIterationWaitsLoopGraphCompletion()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = LoopBehavior("start");
            behavior.LoopPolicy!.RepeatCount = 3;
            behavior.LoopPolicy.IntervalMs = 0;
            using var host = CreateHost(runtime);
            await AttachHost(host, CreateDocument(behavior));
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForExecutionCountAsync(behavior.LoopGraph, 1, TimeSpan.FromSeconds(5));
            await DrainDispatcherAsync();
            Assert.Equal(1, runtime.ExecutedGraphs.Count(graph => graph == behavior.LoopGraph));
            runtime.LoopGate.SetResult();
            await runtime.WaitForExecutionCountAsync(behavior.LoopGraph, 3, TimeSpan.FromSeconds(5));
            Assert.Equal(3, runtime.ExecutedGraphs.Count(graph => graph == behavior.LoopGraph));
        });
    }

    [Fact]
    public async Task Loop_CompleteCurrentIteration_DoesNotCancelCurrentLoopGraph()
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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.CompleteCurrentIteration
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            // Fire StopTrigger while LoopGraph is blocked — CompleteCurrentIteration
            // should NOT cancel the CTS, allowing the current iteration to finish.
            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            // Release the LoopGate so the current iteration completes
            runtime.LoopGate.TrySetResult();

            // Wait for StopGraph to appear, confirming the lifecycle executed
            // StopGraph after the current LoopGraph iteration completed.
            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));

            var executedGraphs = runtime.ExecutedGraphs.ToArray();
            Assert.Contains(behavior.LoopGraph, executedGraphs);
            Assert.Contains(behavior.StopGraph, executedGraphs);
        });
    }

    /// <summary>
    /// StartGraph 执行期间收到 StopTrigger（RunStopGraph 模式），
    /// StartGraph 被取消后仍执行 StopGraph。
    /// </summary>
    [Fact]
    public async Task Loop_StopTrigger_DuringStartGraph_StillExecutesStopGraph()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                // Block during StartGraph execution so StopTrigger can fire while Starting
                StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
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

            // Start trigger fires — StartGraph begins, blocks on StartGate
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await DrainDispatcherAsync();

            // StopTrigger fires while StartGraph is still executing (LoopPhase = Starting).
            // RunStopGraph mode cancels StartGraph via StartCts, then proceeds to StopGraph.
            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            // StartCts cancellation unblocks StartGate → StartGraph returns Cancelled.
            // The lifecycle swallows this and proceeds to StopGraph.

            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));

            Assert.Contains(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    /// <summary>
    /// StopMode=HoldCurrentState 时，收到 StopTrigger 后不取消 LoopCts，
    /// 让当前 LoopGraph 迭代自然完成（或阻塞等待），不执行 StopGraph。
    /// </summary>
    [Fact]
    public async Task Loop_HoldCurrentState_DoesNotCancelLoopGraph()
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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.HoldCurrentState,
                    ResetOnStop = false
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            // Fire StopTrigger while LoopGraph is blocked on LoopGate
            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            // HoldCurrentState should NOT cancel LoopCts → LoopGate should not be cancelled
            await DrainDispatcherAsync();
            Assert.False(runtime.LoopGate.Task.IsCanceled,
                "HoldCurrentState should not cancel LoopCts");

            // Release the gate so the current iteration can complete
            runtime.LoopGate.TrySetResult();
            await DrainDispatcherAsync();

            // StopGraph should NOT be executed for HoldCurrentState
            Assert.DoesNotContain(behavior.StopGraph, runtime.ExecutedGraphs);
        });
    }

    /// <summary>
    /// StopMode=HoldCurrentState 时，停止后不调用 ResetTarget，保持当前动画状态。
    /// </summary>
    [Fact]
    public async Task Loop_HoldCurrentState_DoesNotReset()
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
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.HoldCurrentState,
                    ResetOnStop = true
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime, animationRuntime);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            // Wait for lifecycle to complete (StopGraph should NOT be executed)
            await DrainDispatcherAsync();

            // HoldCurrentState should NOT call ResetTarget
            Assert.Empty(animationRuntime.ResetTargetCalls);
        });
    }

    /// <summary>
    /// StartGraph 中包含 Delay 时，Delay 完成后才进入 LoopGraph。
    /// 使用 StartGate 模拟 StartGraph 中的延迟阻塞，验证 LoopGraph 在 StartGraph 完成前不被执行。
    /// </summary>
    [Fact]
    public async Task Loop_StartGraph_DelayBlocksBeforeLoopGraph()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                // Block StartGraph execution to simulate a Delay inside StartGraph
                StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = new FrontedNodeGraph(),
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = 1,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = false
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHost(runtime);
            await AttachHost(host, document);

            // Fire start trigger — StartGraph begins, blocks on StartGate
            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await DrainDispatcherAsync();

            // StartGraph is still blocked by the simulated Delay;
            // LoopGraph should NOT have been executed yet
            Assert.Contains(behavior.StartGraph, runtime.ExecutedGraphs);
            Assert.DoesNotContain(behavior.LoopGraph, runtime.ExecutedGraphs);

            // Release the StartGate (simulating Delay completion)
            runtime.StartGate.TrySetResult();

            // Wait for the lifecycle to complete (RepeatCount=1, then lifecycle ends)
            await WaitForGraphAsync(runtime, behavior.LoopGraph, TimeSpan.FromSeconds(5));

            // Now LoopGraph should have been executed
            Assert.Contains(behavior.LoopGraph, runtime.ExecutedGraphs);

            // Verify execution order: StartGraph before LoopGraph
            var executedGraphs = runtime.ExecutedGraphs.ToArray();
            var startIndex = Array.IndexOf(executedGraphs, behavior.StartGraph);
            var loopIndex = Array.IndexOf(executedGraphs, behavior.LoopGraph);
            Assert.True(startIndex >= 0, "StartGraph should be executed");
            Assert.True(loopIndex >= 0, "LoopGraph should be executed");
            Assert.True(startIndex < loopIndex, "StartGraph should execute before LoopGraph");
        });
    }

    /// <summary>
    /// StopGraph 没有 Start 节点时，跳过 StopGraph 并设置 SuppressReset，
    /// 防止 Reset 覆盖动画状态导致用户困惑。
    /// </summary>
    [Fact]
    public async Task Loop_StopGraphNoStartNode_SuppressesReset()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var animationRuntime = new RecordingAnimationRuntime();
            // StopGraph with nodes but no flow.start → validation fails
            var stopGraph = new FrontedNodeGraph();
            stopGraph.Nodes.Add(new FrontedNode { NodeType = "flow.end", X = 100, Y = 100 });
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = stopGraph,
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

            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            // Wait for lifecycle to complete (StopGraph won't execute; SuppressReset skips Reset)
            await DrainDispatcherAsync();

            // No ResetTarget because SuppressReset = true
            Assert.Empty(animationRuntime.ResetTargetCalls);
        });
    }

    /// <summary>
    /// StopGraph 包含 WaitForCompletion=false 的 animateProperty 节点时记录 Warning。
    /// </summary>
    [Fact]
    public async Task Loop_StopGraphFireAndForget_LogsWarning()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var runtime = new ControlledGraphRuntime
            {
                LoopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var testLogger = new TestLogger();
            // StopGraph with flow.start → flow.end + action.animateProperty node (WaitForCompletion=false)
            using var doc = System.Text.Json.JsonDocument.Parse("false");
            var ffNode = new FrontedNode
            {
                NodeType = "action.animateProperty",
                X = 200,
                Y = 100,
                Properties = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["WaitForCompletion"] = doc.RootElement.Clone()
                }
            };
            var stopGraph = new FrontedNodeGraph();
            stopGraph.Nodes.Add(new FrontedNode { NodeType = "flow.start", X = 60, Y = 100 });
            stopGraph.Nodes.Add(new FrontedNode { NodeType = "flow.end", X = 360, Y = 100 });
            stopGraph.Nodes.Add(ffNode);
            var behavior = new FrontedBehavior
            {
                Kind = FrontedBehaviorKind.Loop,
                StartTrigger = new TriggerDescriptor { EventType = "start" },
                StopTriggers = [new TriggerDescriptor { EventType = "end" }],
                StartGraph = new FrontedNodeGraph(),
                LoopGraph = new FrontedNodeGraph(),
                StopGraph = stopGraph,
                LoopPolicy = new FrontedLoopPolicy
                {
                    RepeatCount = -1,
                    StopMode = FrontedLoopStopMode.RunStopGraph,
                    ResetOnStop = false
                }
            };
            var document = CreateDocument(behavior);

            using var host = CreateHostWithLogger(runtime, testLogger);
            await AttachHost(host, document);

            RunEvent(host, new FrontedBehaviorEvent { EventType = "start" });
            await runtime.WaitForStartGraphAsync(TimeSpan.FromSeconds(5));

            RunEvent(host, new FrontedBehaviorEvent { EventType = "end" });

            await WaitForGraphAsync(runtime, behavior.StopGraph, TimeSpan.FromSeconds(5));

            var warnings = testLogger.LogEntries
                .Where(e => e.Level == LogLevel.Warning)
                .Select(e => e.Message)
                .ToArray();
            Assert.Contains(warnings, w => w.Contains("WaitForCompletion=false"));
        });
    }

    // ---------------------------------------------------------------
    // Test helper: waits for a specific graph to appear in ExecutedGraphs
    // ---------------------------------------------------------------

    private static Task WaitForGraphAsync(ControlledGraphRuntime runtime, FrontedNodeGraph graph, TimeSpan timeout) =>
        runtime.WaitForExecutionCountAsync(graph, 1, timeout);

    private static Task DrainDispatcherAsync() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(
            () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle).Task;

    private static FrontedNodeConnection Connect(
        FrontedNode source,
        string sourcePort,
        FrontedNode target,
        string targetPort) =>
        new()
        {
            SourceNodeId = source.NodeId,
            SourcePort = sourcePort,
            TargetNodeId = target.NodeId,
            TargetPort = targetPort
        };

    // ---------------------------------------------------------------
    // STA thread helper
    // ---------------------------------------------------------------

    /// <summary>
    /// 在 STA 线程上运行给定的异步操作，WPF 控件创建需要这样做。
    /// </summary>
    private static async Task RunOnStaThreadAsync(Func<Task> action)
    {
        await WpfTestThread.RunAsync(action);
    }

    private static TestHost CreateHost(
        IFrontedNodeGraphRuntime graphRuntime,
        IFrontedAnimationRuntime? animationRuntime = null,
        Canvas? rootCanvas = null,
        ILogger? logger = null)
    {
        var context = new FrontedBehaviorRuntimeContext
        {
            WindowId = "TestWindow",
            WindowType = "BpWindow",
            CanvasName = "BaseCanvas",
            RootCanvas = rootCanvas ?? new Canvas(),
            WindowConfig = FrontedWindowConfigCanvasAdapter.FromCanvasConfig(new FrontedCanvasConfig()),
            SharedDataService = Moq.Mock.Of<ISharedDataService>(),
            Logger = logger ?? NullLogger.Instance,
            IsDesignerPreview = true
        };
        var events = new MockEventBus();
        return new TestHost(new FrontedBehaviorRuntimeHost(context, events, graphRuntime,
            animationRuntime ?? new RecordingAnimationRuntime(), new FrontedBehaviorTriggerEvaluator()), events);
    }

    private static TestHost CreateHostWithLogger(IFrontedNodeGraphRuntime graphRuntime, ILogger logger) =>
        CreateHost(graphRuntime, logger: logger);

    private static Task AttachHost(TestHost host, FrontedBehaviorDocument document) => host.Runtime.AttachAsync(document);
    private static void RunEvent(TestHost host, FrontedBehaviorEvent behaviorEvent) => host.Events.Publish(behaviorEvent);
    private static Task<int> StopAllLoopsAsync(TestHost host, FrontedBehaviorStopReason reason, TimeSpan timeout) =>
        host.Runtime.StopAllLoopBehaviorsAsync(reason, timeout, TestContext.Current.CancellationToken);

    private sealed class TestHost(FrontedBehaviorRuntimeHost runtime, MockEventBus events) : IDisposable
    {
        /// <summary>被测行为运行时。</summary>
        public FrontedBehaviorRuntimeHost Runtime { get; } = runtime;
        /// <summary>测试持有的事件入口。</summary>
        public MockEventBus Events { get; } = events;
        /// <summary>取消并释放行为运行时。</summary>
        public void Dispose() => Runtime.Dispose();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static FrontedBehaviorDocument CreateDocument(params FrontedBehavior[] behaviors)
    {
        return new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = "BpWindow",
            CanvasName = "TestCanvas",
            ControlBehaviorSets = behaviors
                .Select(behavior => new ControlBehaviorSet
                {
                    BehaviorGuid = behavior.BehaviorId,
                    DisplayName = "TestControl",
                    Behaviors = [behavior]
                })
                .ToList()
        };
    }

    private static FrontedBehavior LoopBehavior(string startEventType) =>
        new()
        {
            Kind = FrontedBehaviorKind.Loop,
            StartTrigger = new TriggerDescriptor { EventType = startEventType },
            StopTriggers = [new TriggerDescriptor { EventType = "end" }],
            StartGraph = new FrontedNodeGraph(),
            LoopGraph = new FrontedNodeGraph(),
            StopGraph = new FrontedNodeGraph(),
            LoopPolicy = new FrontedLoopPolicy
            {
                RepeatCount = -1,
                IntervalMs = 100000,
                StopMode = FrontedLoopStopMode.RunStopGraph,
                ResetOnStop = false
            }
        };

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

    /// <summary>
    /// 用于 Loop 行为测试的 <see cref="IFrontedNodeGraphRuntime" /> 受控实现。
    /// 跟踪执行过的图，并支持在 LoopGraph 上阻塞以进行 StopTrigger 测试。
    /// </summary>
    private sealed class ControlledGraphRuntime : IFrontedNodeGraphRuntime
    {
        /// <summary>已执行的图，按顺序排列。</summary>
        public List<FrontedNodeGraph> ExecutedGraphs { get; } = [];

        private readonly Dictionary<(FrontedNodeGraph Graph, int Count), TaskCompletionSource> _executionSignals = [];

        /// <summary>等待指定图开始指定次数，不使用后台轮询。</summary>
        /// <param name="graph">待观察的图。</param>
        /// <param name="count">期望执行次数。</param>
        /// <param name="timeout">等待上限。</param>
        /// <returns>达到执行次数时完成的任务。</returns>
        public Task WaitForExecutionCountAsync(FrontedNodeGraph graph, int count, TimeSpan timeout)
        {
            if (ExecutedGraphs.Count(item => ReferenceEquals(item, graph)) >= count)
                return Task.CompletedTask;
            var key = (graph, count);
            if (!_executionSignals.TryGetValue(key, out var signal))
                _executionSignals[key] = signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return signal.Task.WaitAsync(timeout);
        }

        /// <summary>图执行记录以及提供给它们的上下文。</summary>
        public List<(FrontedNodeGraph Graph, FrontedGraphExecutionContext Context)> Executions { get; } = [];

        /// <summary>
        /// 非 null 时，任意图的执行都会在此门控上阻塞。
        /// 由 <see cref="FrontedBehaviorRuntimeHost.ExecuteLoopLifecycleAsync" /> 使用，
        /// 用于保持 LoopGraph 处于"运行中"状态，以便触发 StopTrigger 测试。
        /// </summary>
        public TaskCompletionSource? LoopGate { get; set; }

        /// <summary>
        /// 非 null 时，首次图执行（StartGraph）会在此门控上阻塞。
        /// 用于测试 StopTrigger 在 StartGraph 执行期间到达的场景。
        /// </summary>
        public TaskCompletionSource? StartGate { get; set; }

        /// <summary>
        /// 非 null 时，在记录一次执行后发出完成信号。
        /// 在触发事件之前设置为一个新 TCS；断言完成后再让其完成。
        /// </summary>
        public TaskCompletionSource? ExecutionCompleted { get; set; }

        /// <summary>
        /// 非 null 时，在任意图首次执行时被设置。
        /// </summary>
        public TaskCompletionSource? StartGraphExecuted { get; set; }

        /// <summary>
        /// 首次执行返回的状态，默认成功。
        /// </summary>
        public FrontedGraphExecutionStatus FirstExecutionStatus { get; set; } = FrontedGraphExecutionStatus.Success;

        public async Task<FrontedGraphExecutionResult> ExecuteAsync(
            FrontedNodeGraph graph,
            FrontedGraphExecutionContext context,
            CancellationToken cancellationToken)
        {
            ExecutedGraphs.Add(graph);
            var executionCount = ExecutedGraphs.Count(item => ReferenceEquals(item, graph));
            if (_executionSignals.TryGetValue((graph, executionCount), out var signal)) signal.TrySetResult();
            Executions.Add((graph, context));

            // Signal StartGraph execution
            if (StartGraphExecuted is not null)
            {
                StartGraphExecuted.TrySetResult();
            }

            // Block during the first execution (StartGraph) when StartGate is set.
            // Used to test StopTrigger arriving while StartGraph is in progress.
            if (StartGate is not null && ExecutedGraphs.Count == 1)
            {
                using var registration = cancellationToken.Register(() =>
                {
                    try { StartGate.TrySetCanceled(cancellationToken); } catch { }
                });
                try
                {
                    await StartGate.Task;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Normal cancellation — execution completed signal still fires below
                }
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

            return new FrontedGraphExecutionResult
            {
                Status = ExecutedGraphs.Count == 1
                    ? FirstExecutionStatus
                    : FrontedGraphExecutionStatus.Success
            };
        }

        /// <summary>
        /// 等待，直到某个图至少被执行过一次。
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

    private sealed class BlockingStopGraphRuntime : IFrontedNodeGraphRuntime
    {
        private int _executionCount;

        public TaskCompletionSource LoopStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource StopGraphStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<FrontedGraphExecutionResult> ExecuteAsync(
            FrontedNodeGraph graph,
            FrontedGraphExecutionContext context,
            CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _executionCount);
            if (count == 2)
            {
                LoopStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Cancelled };
                }
            }

            if (count == 3)
            {
                StopGraphStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Cancelled };
                }
            }

            return new FrontedGraphExecutionResult { Status = FrontedGraphExecutionStatus.Success };
        }
    }

    /// <summary>
    /// 记录 <see cref="IFrontedAnimationRuntime.ResetTarget" /> 的调用。
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

    /// <summary>
    /// 简单的 <see cref="ILogger"/> 实现，捕获日志条目用于测试断言。
    /// </summary>
    private sealed class TestLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> LogEntries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add((logLevel, formatter(state, exception)));
        }
    }
}
