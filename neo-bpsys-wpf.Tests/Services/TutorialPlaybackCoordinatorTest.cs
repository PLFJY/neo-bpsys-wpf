using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Verifies tutorial playback serialization, cleanup, and child-window handoff behavior.
/// </summary>
public sealed class TutorialPlaybackCoordinatorTest
{
    /// <summary>
    /// Verifies a completed sequence does not suppress a later run with the same identity.
    /// </summary>
    [Fact]
    public async Task CompletedSequenceCanRunAgain()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(NullLogger<TutorialPlaybackCoordinator>.Instance);
            var owner = new FrameworkElement();
            var runCount = 0;

            await coordinator.RunSequenceAsync(owner, "Cleanup", Run, CancellationToken.None);
            await coordinator.RunSequenceAsync(owner, "Cleanup", Run, CancellationToken.None);

            Assert.Equal(2, runCount);
            return;

            Task<TutorialRunResult> Run(CancellationToken _)
            {
                runCount++;
                return Task.FromResult(TutorialRunResult.Completed);
            }
        }, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Verifies cancellation and failure both release the sequence identity for retry.
    /// </summary>
    [Fact]
    public async Task CanceledOrFailedSequenceCanBeRetried()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(NullLogger<TutorialPlaybackCoordinator>.Instance);
            var owner = new FrameworkElement();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            Assert.Equal(
                TutorialRunResult.Canceled,
                await coordinator.RunSequenceAsync(owner, "Canceled", _ => Task.FromResult(TutorialRunResult.Completed), canceled.Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                coordinator.RunSequenceAsync(owner, "Failed", _ => throw new InvalidOperationException("test"), CancellationToken.None));

            Assert.Equal(
                TutorialRunResult.Completed,
                await coordinator.RunSequenceAsync(owner, "Canceled", _ => Task.FromResult(TutorialRunResult.Completed), CancellationToken.None));
            Assert.Equal(
                TutorialRunResult.Completed,
                await coordinator.RunSequenceAsync(owner, "Failed", _ => Task.FromResult(TutorialRunResult.Completed), CancellationToken.None));
        }, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Verifies playback requests for one window are serialized.
    /// </summary>
    [Fact]
    public async Task SameWindowSerializesPlayback()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = new TutorialPlaybackCoordinator(NullLogger<TutorialPlaybackCoordinator>.Instance);
            var window = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            window.Show();
            try
            {
                var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var secondStarted = false;
                var first = coordinator.RunAsync(window, "First", async _ =>
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task;
                    return TutorialRunResult.Completed;
                });
                await firstStarted.Task;

                var second = coordinator.RunAsync(window, "Second", _ =>
                {
                    secondStarted = true;
                    return Task.FromResult(TutorialRunResult.Completed);
                });
                await Task.Yield();
                Assert.False(secondStarted);

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

    /// <summary>
    /// Verifies a child tutorial temporarily owns playback and then resumes the parent sequence.
    /// </summary>
    [Fact]
    public async Task ChildWindowHandoffResumesParentSequence()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var cancellation = new TestStepCancellation();
            var coordinator = new TutorialPlaybackCoordinator(
                NullLogger<TutorialPlaybackCoordinator>.Instance,
                cancellation);
            var parent = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            var child = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            parent.Show();
            try
            {
                child.Owner = parent;
                var parentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var parentRuns = 0;
                var parentTask = coordinator.RunSequenceAsync(parent, "Parent", async _ =>
                {
                    parentRuns++;
                    parentStarted.TrySetResult();
                    if (parentRuns == 1)
                    {
                        try
                        {
                            await Task.Delay(Timeout.Infinite, cancellation.Token);
                        }
                        catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
                        {
                            return TutorialRunResult.ChildWindowHandoff;
                        }
                    }

                    return TutorialRunResult.Completed;
                });
                await parentStarted.Task;

                var session = await coordinator.BeginChildWindowSessionAsync(child);
                Assert.NotNull(session);
                Assert.True(cancellation.CancelCalled);
                child.Show();
                Assert.Equal(
                    TutorialRunResult.Completed,
                    await coordinator.RunAsync(child, "Child", _ => Task.FromResult(TutorialRunResult.Completed)));
                Assert.False(parentTask.IsCompleted);

                session!.Complete();
                Assert.Equal(TutorialRunResult.Completed, await parentTask);
                Assert.Equal(2, parentRuns);
            }
            finally
            {
                child.Close();
                parent.Close();
            }
        }, TimeSpan.FromSeconds(15));
    }

    private sealed class TestStepCancellation : ITutorialStepCancellation
    {
        private readonly CancellationTokenSource _source = new();

        /// <summary>
        /// Gets the token observed by the active tutorial step.
        /// </summary>
        public CancellationToken Token => _source.Token;

        /// <summary>
        /// Gets whether the parent step yielded to a child window.
        /// </summary>
        public bool CancelCalled { get; private set; }

        /// <inheritdoc />
        public void YieldCurrentStepForChildWindow(FrameworkElement owner)
        {
            CancelCalled = true;
            _source.Cancel();
        }
    }
}
