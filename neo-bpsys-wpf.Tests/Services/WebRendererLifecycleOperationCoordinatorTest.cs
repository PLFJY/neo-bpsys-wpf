using System;
using System.Threading;
using System.Threading.Tasks;
using neo_bpsys_wpf.WebRenderer.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>Web Renderer 生命周期操作协调器测试。</summary>
public sealed class WebRendererLifecycleOperationCoordinatorTest
{
    /// <summary>状态通知订阅者抛异常时不得跳过信号量释放或造成后续操作死锁。</summary>
    [Fact]
    public async Task ThrowingStateChangedSubscriberDoesNotBreakSerialization()
    {
        using var coordinator = new WebRendererLifecycleOperationCoordinator();
        coordinator.StateChanged += (_, _) => throw new InvalidOperationException("subscriber failure");

        for (var index = 0; index < 3; index++)
        {
            await coordinator.RunAsync("Probe", TimeSpan.FromSeconds(5), _ => Task.CompletedTask)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.False(coordinator.IsLifecycleOperationRunning);
        Assert.Null(coordinator.CurrentOperation);
    }

    /// <summary>执行超过总超时时应抛出 TimeoutException，并恢复状态、释放信号量。</summary>
    [Fact]
    public async Task TimeoutThrowsTimeoutExceptionAndReleasesGate()
    {
        using var coordinator = new WebRendererLifecycleOperationCoordinator();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            coordinator.RunAsync("Slow", TimeSpan.FromMilliseconds(100),
                async token => await Task.Delay(Timeout.InfiniteTimeSpan, token)));

        Assert.False(coordinator.IsLifecycleOperationRunning);
        await coordinator.RunAsync("AfterTimeout", TimeSpan.FromSeconds(5), _ => Task.CompletedTask)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>调用方取消应保留 OperationCanceledException，而不是转换为超时。</summary>
    [Fact]
    public async Task CallerCancellationIsNotReportedAsTimeout()
    {
        using var coordinator = new WebRendererLifecycleOperationCoordinator();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.RunAsync("Canceled", TimeSpan.FromSeconds(10),
                async token => await Task.Delay(Timeout.InfiniteTimeSpan, token),
                cancellation.Token));
    }

    /// <summary>参数非法时应在入口快速失败。</summary>
    [Fact]
    public async Task RejectsInvalidArguments()
    {
        using var coordinator = new WebRendererLifecycleOperationCoordinator();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            coordinator.RunAsync(" ", TimeSpan.FromSeconds(1), _ => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            coordinator.RunAsync("Probe", TimeSpan.FromSeconds(1), null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            coordinator.RunAsync("Probe", Timeout.InfiniteTimeSpan - TimeSpan.FromMilliseconds(1), _ => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            coordinator.RunAsync("Probe", TimeSpan.Zero, _ => Task.CompletedTask));
    }

    /// <summary>释放后继续调用应抛出 ObjectDisposedException。</summary>
    [Fact]
    public async Task RejectsCallsAfterDispose()
    {
        var coordinator = new WebRendererLifecycleOperationCoordinator();
        coordinator.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            coordinator.RunAsync("Probe", TimeSpan.FromSeconds(1), _ => Task.CompletedTask));
    }
}
