using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>Tests serialized Product Tour playback and persisted sequence state.</summary>
public sealed class ProductTourStateTest
{
    [Fact]
    public async Task RunSequence_ShouldPlayAllPendingPackagesInOrder()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterSequence("Page.Test", "Package.One", "Package.Two", "Package.Three");

            var result = await fixture.Runner.RunSequenceAsync(new FrameworkElement(), "Page.Test");

            Assert.Equal(TutorialRunResult.Completed, result);
            Assert.Equal(["Package.One", "Package.Two", "Package.Three"], fixture.Observer.StartedPackageIds);
        });
    }

    [Fact]
    public async Task RunSequence_ShouldSkipPersistedCompletedPackages()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterSequence("Page.Test", "Package.One", "Package.Two");
            await fixture.StateStore.SaveAsync(new TutorialState
            {
                CompletedPackages =
                {
                    ["Package.One"] = new TutorialCompletionRecord
                    {
                        Version = 1,
                        CompletionKind = TutorialCompletionKind.Completed
                    }
                }
            });

            await fixture.Runner.RunSequenceAsync(new FrameworkElement(), "Page.Test");

            Assert.Equal(["Package.Two"], fixture.Observer.StartedPackageIds);
        });
    }

    [Fact]
    public async Task RunSequence_ShouldResolveNextPackageAfterEachCompletion()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterSequence("Page.Test", "Package.One", "Package.Two");

            await fixture.Runner.RunSequenceAsync(new FrameworkElement(), "Page.Test");

            Assert.Equal(3, fixture.Observer.SequenceResolutionCount);
        });
    }

    [Fact]
    public async Task BusySequenceRequest_ShouldQueueInsteadOfReturnSuppressed()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = CreateCoordinator();
            var firstOwner = new FrameworkElement();
            var secondOwner = new FrameworkElement();
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStarted = false;
            var first = coordinator.RunAsync(firstOwner, "First", async _ =>
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
                return TutorialRunResult.Completed;
            });
            await firstStarted.Task;

            var second = coordinator.RunSequenceAsync(secondOwner, "Second", _ =>
            {
                secondStarted = true;
                return Task.FromResult(TutorialRunResult.Completed);
            });
            await Task.Yield();
            Assert.False(secondStarted);

            releaseFirst.SetResult();
            Assert.Equal(TutorialRunResult.Completed, await first);
            Assert.Equal(TutorialRunResult.Completed, await second);
            Assert.True(secondStarted);
        });
    }

    [Fact]
    public async Task DuplicateOwnerSequenceRequests_ShouldCoalesce()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = CreateCoordinator();
            var owner = new FrameworkElement();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionCount = 0;
            Task<TutorialRunResult> Playback(CancellationToken _) => RunAsync();
            async Task<TutorialRunResult> RunAsync()
            {
                executionCount++;
                await release.Task;
                return TutorialRunResult.Completed;
            }

            var first = coordinator.RunSequenceAsync(owner, "Page.Test", Playback);
            var duplicate = coordinator.RunSequenceAsync(owner, "Page.Test", Playback);
            Assert.Same(first, duplicate);
            release.SetResult();
            await Task.WhenAll(first, duplicate);
            Assert.Equal(1, executionCount);
        });
    }

    [Fact]
    public async Task QueuedSequence_ShouldRespectCancellation()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var coordinator = CreateCoordinator();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = coordinator.RunAsync(new FrameworkElement(), "First", async _ =>
            {
                firstStarted.SetResult();
                await release.Task;
                return TutorialRunResult.Completed;
            });
            await firstStarted.Task;
            using var cts = new CancellationTokenSource();
            var stale = coordinator.RunSequenceAsync(
                new FrameworkElement(),
                "Stale",
                _ => Task.FromResult(TutorialRunResult.Completed),
                cts.Token);
            cts.Cancel();

            Assert.Equal(TutorialRunResult.Canceled, await stale);
            release.SetResult();
            await first;
        });
    }

    [Fact]
    public async Task QueuedSequenceAfterFlow_ShouldRecheckCoveredPackageState()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterSequence("Page.Test", "Package.Covered", "Package.Remaining");
            var flowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.FlowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.Test",
                IncludedPackageIds = ["Package.Covered"],
                Items =
                [
                    new ActionFlowItem
                    {
                        ActionAsync = async (_, _) =>
                        {
                            flowStarted.SetResult();
                            await releaseFlow.Task;
                        }
                    }
                ]
            });
            var window = new Window();
            var flow = fixture.Runner.RunFlowAsync(window, "Flow.Test");
            await flowStarted.Task;
            var sequence = fixture.Runner.RunSequenceAsync(new FrameworkElement(), "Page.Test");

            releaseFlow.SetResult();
            Assert.Equal(TutorialRunResult.Completed, await flow);
            Assert.Equal(TutorialRunResult.Completed, await sequence);
            Assert.Equal(["Package.Remaining"], fixture.Observer.StartedPackageIds);
        });
    }

    [Fact]
    public void OldExecutionApisAndAutoRunStrategy_ShouldNotExist()
    {
        var runnerMethods = typeof(ITutorialRunner).GetMethods().Select(method => method.Name).ToArray();
        Assert.DoesNotContain("TryRunNextPackageAsync", runnerMethods);
        Assert.DoesNotContain("RunUntilBlockedAsync", runnerMethods);
        Assert.DoesNotContain("TryRunPackageAsync", runnerMethods);
        Assert.DoesNotContain("TryRunFlowAsync", runnerMethods);
        Assert.Null(typeof(ITutorialRunner).Assembly.GetType("neo_bpsys_wpf.ProductTour.TutorialAutoRunStrategy"));
        Assert.DoesNotContain("Suppressed", Enum.GetNames<TutorialRunResult>());
    }

    [Fact]
    public void PackageDialogue_ShouldUseDialogueOverlay()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.ProductTour", "TutorialService.cs"));
        Assert.Contains("TutorialPackageDialogueItem dialogueItem", source, StringComparison.Ordinal);
        Assert.Contains("ShowDialogueAsync(owner, dialogueItem.Dialogue", source, StringComparison.Ordinal);
        Assert.Contains("new DialogueOverlay(", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "neo-bpsys-wpf.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static TutorialPlaybackCoordinator CreateCoordinator() =>
        new(NullLogger<TutorialPlaybackCoordinator>.Instance);

    private sealed class Fixture
    {
        public TutorialPackageRegistry PackageRegistry { get; } = new();
        public TutorialSequenceRegistry SequenceRegistry { get; } = new();
        public TutorialFlowRegistry FlowRegistry { get; } = new();
        public InMemoryTutorialStateStore StateStore { get; } = new();
        public RecordingObserver Observer { get; } = new();
        public TutorialRunner Runner { get; }

        public Fixture()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var service = new TutorialService(
                services,
                PackageRegistry,
                SequenceRegistry,
                FlowRegistry,
                StateStore,
                new TutorialSignalService(),
                new DefaultTutorialTextProvider(),
                new NoOpTutorialAvatarProvider(),
                Observer,
                new ProductTourOptions(),
                NullLogger<TutorialService>.Instance);
            Runner = new TutorialRunner(
                service,
                CreateCoordinator(),
                PackageRegistry,
                FlowRegistry,
                StateStore,
                NullLogger<TutorialRunner>.Instance);
        }

        public void RegisterSequence(string tutorialKey, params string[] packageIds)
        {
            foreach (var packageId in packageIds)
            {
                PackageRegistry.Register(new TutorialPackageDefinition
                {
                    PackageId = packageId,
                    PageKey = tutorialKey
                });
            }

            SequenceRegistry.RegisterSequence(tutorialKey, packageIds);
        }
    }

    private sealed class RecordingObserver : ITutorialRunObserver
    {
        public List<string> StartedPackageIds { get; } = [];
        public int SequenceResolutionCount { get; private set; }
        public void OnAutoRunRequested(string ownerType, string pageKey, string reason) { }
        public void OnAutoRunCompleted(string ownerType, string pageKey, TutorialRunResult result) { }
        public void OnPackageRunRequested(string packageId, string pageKey, TutorialTriggerMode triggerMode) { }
        public void OnPackageStarted(string packageId, string pageKey, TutorialTriggerMode triggerMode) => StartedPackageIds.Add(packageId);
        public void OnStepShown(string packageId, string? targetName, string title) { }
        public void OnPackageCompleted(string packageId, TutorialRunResult result) { }
        public void OnPackageNotPending(string pageKey) { }
        public void OnPackageSkippedByState(string packageId, TutorialCompletionKind completionKind, int recordedVersion, int currentVersion) { }
        public void OnPackageNotReady(string packageId, string pageKey) { }
        public void OnSequenceResolved(string pageKey, IReadOnlyList<string> packageIds) => SequenceResolutionCount++;
        public void OnPackageTargetMissing(string packageId) { }
    }

    private sealed class InMemoryTutorialStateStore : ITutorialStateStore
    {
        private TutorialState _state = new();
        public Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Clone(_state));
        public Task SaveAsync(TutorialState state, CancellationToken cancellationToken = default)
        {
            _state = Clone(state);
            return Task.CompletedTask;
        }
        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            _state = new TutorialState();
            return Task.CompletedTask;
        }
        private static TutorialState Clone(TutorialState state) => new()
        {
            CompletedFlows = state.CompletedFlows.ToDictionary(pair => pair.Key, pair => Clone(pair.Value)),
            CompletedPackages = state.CompletedPackages.ToDictionary(pair => pair.Key, pair => Clone(pair.Value))
        };
        private static TutorialCompletionRecord Clone(TutorialCompletionRecord record) => new()
        {
            Version = record.Version,
            CompletionKind = record.CompletionKind,
            SourceFlowId = record.SourceFlowId,
            CompletedAt = record.CompletedAt
        };
    }
}
