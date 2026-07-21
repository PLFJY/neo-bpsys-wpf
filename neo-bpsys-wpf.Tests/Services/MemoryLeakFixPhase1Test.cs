#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 阶段 1 内存泄漏修复验证测试。
/// 验证 TutorialPlaybackCoordinator 不再永久持有窗口、Designer 窗口在关闭时取消语言事件订阅。
/// </summary>
public sealed class MemoryLeakFixPhase1Test
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. Coordinator gate 不形成永久强引用
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 <see cref="TutorialPlaybackCoordinator"/> 的 <c>_playbackGates</c>
    /// 字段类型为 <see cref="ConditionalWeakTable{TKey, TValue}"/>，
    /// 确保已关闭窗口可被 GC 回收，不被 Singleton 协调器永久持有。
    /// </summary>
    [Fact]
    public void CoordinatorPlaybackGates_IsConditionalWeakTable()
    {
        var field = typeof(TutorialPlaybackCoordinator).GetField(
            "_playbackGates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Equal(
            typeof(ConditionalWeakTable<object, SemaphoreSlim>),
            field!.FieldType);
    }

    /// <summary>
    /// 验证播放完成后，同一窗口可以再次播放（gate 复用语义正常），
    /// 且 gate 存储在 ConditionalWeakTable 中不阻止窗口 GC。
    /// 注意：WPF Window 因 Dispatcher/HWND 内部引用，Close() 后不一定立即可 GC，
    /// 因此此测试验证行为契约（复用 + 不抛异常），GC 语义由字段类型断言保证。
    /// </summary>
    [Fact]
    public async Task CoordinatorGate_ReusesGateForSameWindowAcrossPlayback()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(
                NullLogger<TutorialPlaybackCoordinator>.Instance);

            var window = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            window.Show();
            try
            {
                var first = await coordinator.RunAsync(
                    window,
                    "First",
                    _ => Task.FromResult(TutorialRunResult.Completed),
                    CancellationToken.None);
                Assert.Equal(TutorialRunResult.Completed, first);

                // 同一窗口再次播放应正常工作，证明 gate 被复用而非泄漏。
                var second = await coordinator.RunAsync(
                    window,
                    "Second",
                    _ => Task.FromResult(TutorialRunResult.Completed),
                    CancellationToken.None);
                Assert.Equal(TutorialRunResult.Completed, second);
            }
            finally
            {
                window.Close();
            }
        }, TimeSpan.FromSeconds(15));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. _sequenceJobs 在正常结束、取消、异常后清理
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证序列正常完成后，<c>_sequenceJobs</c> 不再保留该 key，
    /// 后续相同 owner + tutorialKey 的请求会实际执行而非返回旧任务。
    /// </summary>
    [Fact]
    public async Task CoordinatorSequenceJobs_ClearedAfterNormalCompletion()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(
                NullLogger<TutorialPlaybackCoordinator>.Instance);
            var owner = new FrameworkElement();
            var runCount = 0;

            await coordinator.RunSequenceAsync(
                owner,
                "CleanupTest",
                _ =>
                {
                    runCount++;
                    return Task.FromResult(TutorialRunResult.Completed);
                },
                CancellationToken.None);

            await coordinator.RunSequenceAsync(
                owner,
                "CleanupTest",
                _ =>
                {
                    runCount++;
                    return Task.FromResult(TutorialRunResult.Completed);
                },
                CancellationToken.None);

            Assert.Equal(2, runCount);
        }, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// 验证序列被取消后，<c>_sequenceJobs</c> 正确清理。
    /// </summary>
    [Fact]
    public async Task CoordinatorSequenceJobs_ClearedAfterCancellation()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(
                NullLogger<TutorialPlaybackCoordinator>.Instance);
            var owner = new FrameworkElement();

            // 使用预先取消的 token，序列在获取 gate 前就被取消。
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var cancelled = await coordinator.RunSequenceAsync(
                owner,
                "CancelledJob",
                _ => Task.FromResult(TutorialRunResult.Completed),
                cts.Token);
            Assert.Equal(TutorialRunResult.Canceled, cancelled);

            // 取消后 _sequenceJobs 应清理，新请求应能实际执行而非返回旧任务。
            var secondRan = false;
            var second = await coordinator.RunSequenceAsync(
                owner,
                "CancelledJob",
                _ =>
                {
                    secondRan = true;
                    return Task.FromResult(TutorialRunResult.Completed);
                },
                CancellationToken.None);
            Assert.True(secondRan);
            Assert.Equal(TutorialRunResult.Completed, second);
        }, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// 验证 <c>playbackAsync</c> 抛出非取消异常时，<c>_sequenceJobs</c> 正确清理。
    /// </summary>
    [Fact]
    public async Task CoordinatorSequenceJobs_ClearedAfterException()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(
                NullLogger<TutorialPlaybackCoordinator>.Instance);
            var owner = new FrameworkElement();

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await coordinator.RunSequenceAsync(
                    owner,
                    "ThrowingJob",
                    _ => throw new InvalidOperationException("test"),
                    CancellationToken.None);
            });

            // 异常后 _sequenceJobs 应清理，新请求应能实际执行。
            var ran = false;
            var result = await coordinator.RunSequenceAsync(
                owner,
                "ThrowingJob",
                _ =>
                {
                    ran = true;
                    return Task.FromResult(TutorialRunResult.Completed);
                },
                CancellationToken.None);
            Assert.True(ran);
            Assert.Equal(TutorialRunResult.Completed, result);
        }, TimeSpan.FromSeconds(15));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. 同一窗口串行播放、不同窗口并行播放（gate 语义不变）
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证同一窗口的多个播放请求仍然串行执行（同一 gate）。
    /// </summary>
    [Fact]
    public async Task CoordinatorSameWindow_StillSerializesPlayback()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(
                NullLogger<TutorialPlaybackCoordinator>.Instance);
            var window = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            window.Show();
            try
            {
                var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var secondStarted = false;

                var first = coordinator.RunAsync(
                    window,
                    "First",
                    async _ =>
                    {
                        firstStarted.TrySetResult();
                        await releaseFirst.Task;
                        return TutorialRunResult.Completed;
                    });
                await firstStarted.Task;

                var second = coordinator.RunAsync(
                    window,
                    "Second",
                    _ =>
                    {
                        secondStarted = true;
                        return Task.FromResult(TutorialRunResult.Completed);
                    });
                await Task.Yield();
                Assert.False(secondStarted, "Second playback on same window should wait for first to complete.");

                releaseFirst.SetResult();
                await Task.WhenAll(first, second);
                Assert.True(secondStarted);
            }
            finally
            {
                window.Close();
            }
        }, TimeSpan.FromSeconds(15));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. ChildWindow handoff 不受 ConditionalWeakTable 改动影响
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 ConditionalWeakTable 改动后，子窗口交接仍然正常工作。
    /// </summary>
    [Fact]
    public async Task CoordinatorChildWindowHandoff_StillWorksAfterFix()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var stepCancellation = new TestStepCancellation();
            var coordinator = new TutorialPlaybackCoordinator(
                NullLogger<TutorialPlaybackCoordinator>.Instance,
                stepCancellation);

            var parentWindow = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            var childWindow = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            parentWindow.Show();
            try
            {
                childWindow.Owner = parentWindow;

                var parentStartedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var childStartedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var parentRunCount = 0;

                var parentTask = coordinator.RunSequenceAsync(
                    parentWindow, "Parent",
                    async _ =>
                    {
                        parentRunCount++;
                        parentStartedTcs.TrySetResult();
                        if (parentRunCount == 1)
                        {
                            try
                            {
                                await Task.Delay(Timeout.Infinite, stepCancellation.Token);
                            }
                            catch (OperationCanceledException) when (stepCancellation.Token.IsCancellationRequested)
                            {
                                return TutorialRunResult.ChildWindowHandoff;
                            }
                        }

                        return TutorialRunResult.Completed;
                    },
                    CancellationToken.None);

                await parentStartedTcs.Task;

                var childSession = await coordinator.BeginChildWindowSessionAsync(childWindow);
                Assert.NotNull(childSession);
                Assert.True(stepCancellation.CancelCalled, "Parent step should be cancelled to yield the gate.");
                childWindow.Show();

                var childTask = coordinator.RunAsync(
                    childWindow, "Child",
                    token =>
                    {
                        childStartedTcs.TrySetResult();
                        return Task.FromResult(TutorialRunResult.Completed);
                    },
                    CancellationToken.None);

                await childStartedTcs.Task;
                await childTask;
                Assert.False(parentTask.IsCompleted);

                childSession!.Complete();

                var parentResult = await parentTask;
                Assert.Equal(TutorialRunResult.Completed, parentResult);
            }
            finally
            {
                childWindow.Close();
                parentWindow.Close();
            }
        }, TimeSpan.FromSeconds(15));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Designer OnClosed 取消 LanguageSettingChanged 订阅（源码契约）
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 <see cref="neo_bpsys_wpf.Views.Windows.FrontedDesignerWindow"/> 的
    /// <c>OnClosed</c> 方法包含对 <c>LanguageSettingChanged</c> 的取消订阅。
    /// </summary>
    [Fact]
    public void DesignerWindow_OnClosed_UnsubscribesLanguageEvent()
    {
        var code = File.ReadAllText(FindRepoFile(
            "neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml.cs"));

        Assert.Contains("_settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged", code);
        Assert.Contains("private readonly ISettingsHostService? _settingsHostService;", code);
    }

    /// <summary>
    /// 验证取消订阅在 <c>OnClosed</c> 中尽可能早地执行，避免后续清理异常导致泄漏。
    /// </summary>
    [Fact]
    public void DesignerWindow_OnClosed_UnsubscribesLanguageEventBeforeOtherCleanup()
    {
        var code = File.ReadAllText(FindRepoFile(
            "neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml.cs"));

        var onClosedStart = code.IndexOf("private void OnClosed(", StringComparison.Ordinal);
        Assert.True(onClosedStart >= 0, "OnClosed method not found.");

        var unsubscribeIndex = code.IndexOf(
            "_settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged",
            onClosedStart,
            StringComparison.Ordinal);
        Assert.True(unsubscribeIndex > onClosedStart, "Unsubscribe not found in OnClosed.");

        // 验证取消订阅在 ViewModel 事件解绑之前。
        var viewModelUnsubscribeIndex = code.IndexOf(
            "_viewModel.PreviewRenderRequested -= OnPreviewRenderRequested",
            onClosedStart,
            StringComparison.Ordinal);
        Assert.True(viewModelUnsubscribeIndex > unsubscribeIndex,
            "Language event unsubscribe should occur before ViewModel event unsubscribe.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string FindRepoFile(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "neo-bpsys-wpf.slnx")))
        {
            current = current.Parent;
        }

        var root = current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        return Path.Combine([root, .. parts]);
    }

    private sealed class TestStepCancellation : ITutorialStepCancellation
    {
        private readonly CancellationTokenSource _cts = new();
        public CancellationToken Token => _cts.Token;
        public bool CancelCalled { get; private set; }

        public void YieldCurrentStepForChildWindow(FrameworkElement owner)
        {
            CancelCalled = true;
            _cts.Cancel();
        }
    }
}
