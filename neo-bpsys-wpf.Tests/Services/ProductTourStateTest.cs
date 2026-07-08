using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.Tutorial;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests Product Tour state transitions that do not depend on real business pages.
/// </summary>
public sealed class ProductTourStateTest
{
    [Fact]
    public async Task PackageCompletedAfterRunIsNotPending()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.Basic", version: 1, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.Basic"]);

            var owner = CreateOwner();
            var completed = await fixture.Service.RunPendingPagePackagesAsync(owner, "Page.Test");
            var pending = await fixture.Service.RunPendingPagePackagesAsync(owner, "Page.Test");

            Assert.Equal(TutorialRunResult.Completed, completed);
            Assert.Equal(TutorialRunResult.NotPending, pending);
        });
    }

    [Fact]
    public async Task AutoRunStrategy_SinglePendingPackage_ShouldRunOnePackagePerTrigger()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.A", version: 1, pageKey: "Page.Test");
            fixture.RegisterPackage("Package.B", version: 1, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.A", "Package.B"]);

            var previousHost = IAppHost.Host;
            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITutorialService>(fixture.Service);
                    services.AddSingleton<ITutorialSequenceRegistry>(fixture.SequenceRegistry);
                })
                .Build();
            IAppHost.Host = host;
            try
            {
                var owner = CreateOwner();
                var window = new Window { Content = owner, Width = 320, Height = 240 };
                window.Show();
                try
                {
                    await InvokeRunPendingOnLoadedAsync(owner, "Page.Test");
                    var stateAfterFirstRun = await fixture.StateStore.LoadAsync();

                    Assert.True(stateAfterFirstRun.CompletedPackages.ContainsKey("Package.A"));
                    Assert.False(stateAfterFirstRun.CompletedPackages.ContainsKey("Package.B"));

                    await InvokeRunPendingOnLoadedAsync(owner, "Page.Test");
                    var stateAfterSecondRun = await fixture.StateStore.LoadAsync();

                    Assert.True(stateAfterSecondRun.CompletedPackages.ContainsKey("Package.A"));
                    Assert.True(stateAfterSecondRun.CompletedPackages.ContainsKey("Package.B"));
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                IAppHost.Host = previousHost;
            }
        });
    }

    [Fact]
    public async Task AutoRunStrategy_DrainSequence_ShouldRunAllPendingPackagesInOneTrigger()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.A", version: 1, pageKey: "Page.Test");
            fixture.RegisterPackage("Package.B", version: 1, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence(
                "Page.Test",
                ["Package.A", "Package.B"],
                TutorialAutoRunStrategy.DrainSequence);

            await RunAutoOnLoadedWithHostAsync(fixture, async owner =>
            {
                await InvokeRunPendingOnLoadedAsync(owner, "Page.Test");
            });

            var state = await fixture.StateStore.LoadAsync();
            Assert.True(state.CompletedPackages.ContainsKey("Package.A"));
            Assert.True(state.CompletedPackages.ContainsKey("Package.B"));
        });
    }

    [Fact]
    public async Task DrainSequence_ShouldStopWhenUserSkips()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage(
                "Package.Interactive",
                version: 1,
                pageKey: "Page.Test",
                steps:
                [
                    new ProductTourStep
                    {
                        Title = "Interactive",
                        Description = "Can skip",
                        Placement = ProductTourPlacement.Center
                    }
                ]);
            fixture.RegisterPackage("Package.AfterSkip", version: 1, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence(
                "Page.Test",
                ["Package.Interactive", "Package.AfterSkip"],
                TutorialAutoRunStrategy.DrainSequence);

            await RunAutoOnLoadedWithHostAsync(fixture, async owner =>
            {
                var runTask = InvokeRunPendingOnLoadedAsync(owner, "Page.Test");
                var overlay = await WaitForOverlayAsync(owner);
                var skipButton = FindButtonByContent(overlay, "跳过");
                Assert.NotNull(skipButton);
                skipButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(50);
                var confirmButton = FindButtonByContent(overlay, "确认跳过");
                Assert.NotNull(confirmButton);
                confirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await runTask;
            });

            var state = await fixture.StateStore.LoadAsync();
            Assert.Equal(TutorialCompletionKind.Skipped, state.CompletedPackages["Package.Interactive"].CompletionKind);
            Assert.False(state.CompletedPackages.ContainsKey("Package.AfterSkip"));
        });
    }

    [Fact]
    public async Task DrainSequence_ShouldResumeUnfinishedPackagesOnNextOpen()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.A", version: 1, pageKey: "Page.Test");
            fixture.RegisterPackage(
                "Package.B",
                version: 1,
                pageKey: "Page.Test",
                steps:
                [
                    new ProductTourStep
                    {
                        TargetName = "DelayedTarget",
                        Title = "Delayed",
                        Description = "Appears next open",
                        Timeout = TimeSpan.FromMilliseconds(500)
                    }
                ]);
            fixture.SequenceRegistry.RegisterSequence(
                "Page.Test",
                ["Package.A", "Package.B"],
                TutorialAutoRunStrategy.DrainSequence);

            await RunAutoOnLoadedWithHostAsync(fixture, async owner =>
            {
                await InvokeRunPendingOnLoadedAsync(owner, "Page.Test");
            });
            var stateAfterFirstOpen = await fixture.StateStore.LoadAsync();
            Assert.True(stateAfterFirstOpen.CompletedPackages.ContainsKey("Package.A"));
            Assert.False(stateAfterFirstOpen.CompletedPackages.ContainsKey("Package.B"));

            await RunAutoOnLoadedWithHostAsync(fixture, async owner =>
            {
                owner.Children.Add(new Border { Name = "DelayedTarget" });
                await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var runTask = InvokeRunPendingOnLoadedAsync(owner, "Page.Test");
                var overlay = await WaitForOverlayAsync(owner);
                var finishButton = FindButtonByContent(overlay, "完成");
                Assert.NotNull(finishButton);
                finishButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await runTask;
            });

            var stateAfterSecondOpen = await fixture.StateStore.LoadAsync();
            Assert.True(stateAfterSecondOpen.CompletedPackages.ContainsKey("Package.B"));
        });
    }

    [Fact]
    public async Task PackageCoveredByFlowIsNotPending()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.Basic", version: 1, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.Basic"]);
            await fixture.StateStore.SaveAsync(new TutorialState
            {
                CompletedPackages =
                {
                    ["Package.Basic"] = new TutorialCompletionRecord
                    {
                        Version = 1,
                        CompletionKind = TutorialCompletionKind.CoveredByFlow,
                        SourceFlowId = "Flow.Test"
                    }
                }
            });

            var result = await fixture.Service.RunPendingPagePackagesAsync(CreateOwner(), "Page.Test");

            Assert.Equal(TutorialRunResult.NotPending, result);
        });
    }

    [Fact]
    public async Task PackageVersionIncreaseBecomesPendingAgain()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.Basic", version: 2, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.Basic"]);
            await fixture.StateStore.SaveAsync(new TutorialState
            {
                CompletedPackages =
                {
                    ["Package.Basic"] = new TutorialCompletionRecord
                    {
                        Version = 1,
                        CompletionKind = TutorialCompletionKind.Completed
                    }
                }
            });

            var result = await fixture.Service.RunPendingPagePackagesAsync(CreateOwner(), "Page.Test");

            Assert.Equal(TutorialRunResult.Completed, result);
            var state = await fixture.StateStore.LoadAsync();
            Assert.Equal(2, state.CompletedPackages["Package.Basic"].Version);
        });
    }

    [Fact]
    public async Task FlowCompletedCoversIncludedPackages()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.Basic", version: 3, pageKey: "Page.Test");
            fixture.FlowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.Test",
                Version = 2,
                IncludedPackageIds = ["Package.Basic"]
            });

            var result = await fixture.Service.RunFlowAsync(CreateOwner(), "Flow.Test");

            Assert.Equal(TutorialRunResult.Completed, result);
            var state = await fixture.StateStore.LoadAsync();
            Assert.Equal(TutorialCompletionKind.Completed, state.CompletedFlows["Flow.Test"].CompletionKind);
            Assert.Equal(TutorialCompletionKind.CoveredByFlow, state.CompletedPackages["Package.Basic"].CompletionKind);
            Assert.Equal("Flow.Test", state.CompletedPackages["Package.Basic"].SourceFlowId);
            Assert.Equal(3, state.CompletedPackages["Package.Basic"].Version);
        });
    }

    [Fact]
    public async Task FlowCoveredPackageIsSkippedButUncoveredPagePackageRemainsPending()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage("Package.Basic", version: 1, pageKey: "Page.Test");
            fixture.RegisterPackage("Package.Advanced", version: 1, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.Basic", "Package.Advanced"]);
            fixture.FlowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.Test",
                Version = 1,
                IncludedPackageIds = ["Package.Basic"]
            });

            var flowResult = await fixture.Service.RunFlowAsync(CreateOwner(), "Flow.Test");
            var pendingResult = await fixture.Service.RunPendingPagePackagesAsync(CreateOwner(), "Page.Test");
            var state = await fixture.StateStore.LoadAsync();

            Assert.Equal(TutorialRunResult.Completed, flowResult);
            Assert.Equal(TutorialRunResult.Completed, pendingResult);
            Assert.Equal(TutorialCompletionKind.CoveredByFlow, state.CompletedPackages["Package.Basic"].CompletionKind);
            Assert.Equal(TutorialCompletionKind.Completed, state.CompletedPackages["Package.Advanced"].CompletionKind);
        });
    }

    [Fact]
    public async Task FlowSkippedDoesNotCoverIncludedPackages()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage(
                "Package.Interactive",
                version: 1,
                pageKey: "Page.Test",
                steps:
                [
                    new ProductTourStep
                    {
                        Title = "Step",
                        Description = "Description",
                        Placement = ProductTourPlacement.Center
                    }
                ]);
            fixture.FlowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.Test",
                Version = 1,
                IncludedPackageIds = ["Package.Interactive"],
                Items = [new PackageFlowItem { PackageId = "Package.Interactive" }]
            });
            var owner = CreateOwner();

            var runTask = fixture.Service.RunFlowAsync(owner, "Flow.Test");
            var overlay = await WaitForOverlayAsync(owner);
            await Task.Delay(350);
            var skipButton = FindButtonByContent(overlay, "跳过");
            Assert.NotNull(skipButton);
            skipButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);
            var confirmButton = FindButtonByContent(overlay, "确认跳过");
            Assert.NotNull(confirmButton);
            confirmButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            var result = await runTask;

            Assert.Equal(TutorialRunResult.Skipped, result);
            var state = await fixture.StateStore.LoadAsync();
            Assert.Equal(TutorialCompletionKind.Skipped, state.CompletedFlows["Flow.Test"].CompletionKind);
            Assert.False(state.CompletedPackages.ContainsKey("Package.Interactive"));
        });
    }

    [Fact]
    public async Task SuppressedDoesNotWriteState()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.RegisterPackage("Package.Basic", version: 1, pageKey: "Page.Test");
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.Basic"]);
            fixture.FlowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.Blocking",
                Items =
                [
                    new ActionFlowItem
                    {
                        ActionAsync = (_, _) => gate.Task
                    }
                ]
            });

            var owner = CreateOwner();
            var flowTask = fixture.Service.RunFlowAsync(owner, "Flow.Blocking");
            var suppressed = await fixture.Service.RunPendingPagePackagesAsync(owner, "Page.Test");
            gate.SetResult();
            await flowTask;

            var state = await fixture.StateStore.LoadAsync();
            Assert.Equal(TutorialRunResult.Suppressed, suppressed);
            Assert.False(state.CompletedPackages.ContainsKey("Package.Basic"));
        });
    }

    [Fact]
    public async Task AllMissingOptionalPackageDoesNotRecordCompleted()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage(
                "Package.OptionalMissing",
                version: 1,
                pageKey: "Page.Test",
                steps:
                [
                    new ProductTourStep
                    {
                        TargetName = "MissingOne",
                        Title = "Missing one",
                        Description = "Optional missing target",
                        AllowMissingTarget = true,
                        Timeout = TimeSpan.FromMilliseconds(20)
                    },
                    new ProductTourStep
                    {
                        TargetName = "MissingTwo",
                        Title = "Missing two",
                        Description = "Optional missing target",
                        AllowMissingTarget = true,
                        Timeout = TimeSpan.FromMilliseconds(20)
                    }
                ]);
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.OptionalMissing"]);

            var result = await fixture.Service.RunPackageAsync(
                CreateOwner(),
                "Package.OptionalMissing",
                TutorialTriggerMode.AutoOnLoaded);
            var state = await fixture.StateStore.LoadAsync();

            Assert.Equal(TutorialRunResult.TargetMissing, result);
            Assert.False(state.CompletedPackages.ContainsKey("Package.OptionalMissing"));
        });
    }

    [Fact]
    public async Task AllMissingOptionalPackageRemainsPendingOnNextPageRun()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage(
                "Package.OptionalMissing",
                version: 1,
                pageKey: "Page.Test",
                steps:
                [
                    new ProductTourStep
                    {
                        TargetName = "MissingTarget",
                        Title = "Missing",
                        Description = "Optional missing target",
                        AllowMissingTarget = true,
                        Timeout = TimeSpan.FromMilliseconds(20)
                    }
                ]);
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.OptionalMissing"]);

            var first = await fixture.Service.RunPendingPagePackagesAsync(CreateOwner(), "Page.Test");
            var second = await fixture.Service.RunPendingPagePackagesAsync(CreateOwner(), "Page.Test");
            var state = await fixture.StateStore.LoadAsync();

            Assert.Equal(TutorialRunResult.TargetMissing, first);
            Assert.Equal(TutorialRunResult.TargetMissing, second);
            Assert.False(state.CompletedPackages.ContainsKey("Package.OptionalMissing"));
        });
    }

    [Fact]
    public async Task CanRunReceivesOwner()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            var expectedOwner = CreateOwner();
            FrameworkElement? receivedOwner = null;
            fixture.PackageRegistry.Register(new TutorialPackageDefinition
            {
                PackageId = "Package.OwnerAware",
                Version = 1,
                PageKey = "Page.Test",
                CanRunWithOwner = (_, owner) =>
                {
                    receivedOwner = owner;
                    return ReferenceEquals(owner, expectedOwner);
                }
            });
            fixture.SequenceRegistry.RegisterSequence("Page.Test", ["Package.OwnerAware"]);

            var result = await fixture.Service.RunPendingPagePackagesAsync(expectedOwner, "Page.Test");

            Assert.Equal(TutorialRunResult.Completed, result);
            Assert.Same(expectedOwner, receivedOwner);
        });
    }

    [Fact]
    public async Task SignalTimeoutDoesNotCompleteUntilUserContinues()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage(
                "Package.Signal",
                version: 1,
                pageKey: "Page.Test",
                steps:
                [
                    new ProductTourStep
                    {
                        Title = "Signal step",
                        Description = "Waits for signal",
                        WaitForSignalId = "Signal.Missing",
                        Timeout = TimeSpan.FromMilliseconds(120),
                        Placement = ProductTourPlacement.Center
                    }
                ]);
            var owner = CreateOwner();

            var runTask = fixture.Service.RunPackageAsync(owner, "Package.Signal", TutorialTriggerMode.Manual);
            var overlay = await WaitForOverlayAsync(owner);
            await Task.Delay(500);
            Assert.False(runTask.IsCompleted);

            var continueButton = FindButtonByContent(overlay, "继续");
            Assert.NotNull(continueButton);
            continueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var result = await runTask;
            Assert.Equal(TutorialRunResult.Completed, result);
        });
    }

    [Fact]
    public async Task ClearFlowStateRemovesCoveredPackagesFromSameFlow()
    {
        var fixture = new Fixture();
        await fixture.StateStore.SaveAsync(new TutorialState
        {
            CompletedFlows =
            {
                ["Flow.Test"] = new TutorialCompletionRecord
                {
                    Version = 1,
                    CompletionKind = TutorialCompletionKind.Completed
                }
            },
            CompletedPackages =
            {
                ["Package.Covered"] = new TutorialCompletionRecord
                {
                    Version = 1,
                    CompletionKind = TutorialCompletionKind.CoveredByFlow,
                    SourceFlowId = "Flow.Test"
                },
                ["Package.OtherFlow"] = new TutorialCompletionRecord
                {
                    Version = 1,
                    CompletionKind = TutorialCompletionKind.CoveredByFlow,
                    SourceFlowId = "Flow.Other"
                },
                ["Package.Completed"] = new TutorialCompletionRecord
                {
                    Version = 1,
                    CompletionKind = TutorialCompletionKind.Completed
                }
            }
        });

        await fixture.Service.ClearFlowStateAsync("Flow.Test");

        var state = await fixture.StateStore.LoadAsync();
        Assert.False(state.CompletedFlows.ContainsKey("Flow.Test"));
        Assert.False(state.CompletedPackages.ContainsKey("Package.Covered"));
        Assert.True(state.CompletedPackages.ContainsKey("Package.OtherFlow"));
        Assert.True(state.CompletedPackages.ContainsKey("Package.Completed"));
    }

    [Fact]
    public async Task FlowTargetMissingDoesNotRecordSkipped()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.RegisterPackage(
                "Package.MissingTarget",
                version: 1,
                pageKey: "Page.Test",
                steps:
                [
                    new ProductTourStep
                    {
                        TargetName = "MissingTarget",
                        Title = "Missing",
                        Description = "Missing target",
                        Timeout = TimeSpan.FromMilliseconds(20)
                    }
                ]);
            fixture.FlowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.MissingTarget",
                Items = [new PackageFlowItem { PackageId = "Package.MissingTarget" }]
            });

            var result = await fixture.Service.RunFlowAsync(CreateOwner(), "Flow.MissingTarget");
            var state = await fixture.StateStore.LoadAsync();

            Assert.Equal(TutorialRunResult.TargetMissing, result);
            Assert.False(state.CompletedFlows.ContainsKey("Flow.MissingTarget"));
            Assert.False(state.CompletedPackages.ContainsKey("Package.MissingTarget"));
        });
    }

    [Fact]
    public async Task FlowCanceledDoesNotRecordSkipped()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var fixture = new Fixture();
            fixture.FlowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.Canceled",
                Items =
                [
                    new ActionFlowItem
                    {
                        ActionAsync = (_, _) => throw new OperationCanceledException()
                    }
                ]
            });

            var result = await fixture.Service.RunFlowAsync(CreateOwner(), "Flow.Canceled");
            var state = await fixture.StateStore.LoadAsync();

            Assert.Equal(TutorialRunResult.Canceled, result);
            Assert.False(state.CompletedFlows.ContainsKey("Flow.Canceled"));
        });
    }

    private static Grid CreateOwner() =>
        new()
        {
            Width = 800,
            Height = 600
        };

    private static Task InvokeRunPendingOnLoadedAsync(FrameworkElement owner, string pageKey)
    {
        var method = typeof(TutorialPageLoader).GetMethod(
            "RunPendingOnLoadedAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (Task)method.Invoke(null, [owner, pageKey])!;
    }

    private static async Task RunAutoOnLoadedWithHostAsync(Fixture fixture, Func<Grid, Task> action)
    {
        var previousHost = IAppHost.Host;
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITutorialService>(fixture.Service);
                services.AddSingleton<ITutorialSequenceRegistry>(fixture.SequenceRegistry);
            })
            .Build();
        IAppHost.Host = host;
        try
        {
            var owner = CreateOwner();
            var window = new Window { Content = owner, Width = 320, Height = 240 };
            window.Show();
            try
            {
                await action(owner);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            IAppHost.Host = previousHost;
        }
    }

    private static async Task<ProductTourOverlay> WaitForOverlayAsync(Panel owner)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested)
        {
            if (owner.Children.OfType<ProductTourOverlay>().FirstOrDefault() is { } overlay)
            {
                return overlay;
            }

            await Task.Delay(20, cts.Token);
        }

        throw new TimeoutException("ProductTourOverlay was not added to the owner.");
    }

    private static Button? FindButtonByContent(DependencyObject root, string content)
    {
        if (root is Button button && Equals(button.Content, content))
        {
            return button;
        }

        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var result = FindButtonByContent(System.Windows.Media.VisualTreeHelper.GetChild(root, i), content);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Service = new TutorialService(
                new EmptyServiceProvider(),
                PackageRegistry,
                SequenceRegistry,
                FlowRegistry,
                StateStore,
                new TutorialSignalService(),
                new DefaultTutorialTextProvider(),
                new NoOpTutorialAvatarProvider(),
                new NoOpTutorialRunObserver(),
                new ProductTourOptions(),
                NullLogger<TutorialService>.Instance);
        }

        public TutorialPackageRegistry PackageRegistry { get; } = new();

        public TutorialSequenceRegistry SequenceRegistry { get; } = new();

        public TutorialFlowRegistry FlowRegistry { get; } = new();

        public InMemoryTutorialStateStore StateStore { get; } = new();

        public TutorialService Service { get; }

        public void RegisterPackage(
            string packageId,
            int version,
            string pageKey,
            IReadOnlyList<ProductTourStep>? steps = null)
        {
            PackageRegistry.Register(new TutorialPackageDefinition
            {
                PackageId = packageId,
                Version = version,
                PageKey = pageKey,
                Steps = steps ?? []
            });
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class InMemoryTutorialStateStore : ITutorialStateStore
    {
        private TutorialState _state = new();

        public Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Clone(_state));

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

        private static TutorialState Clone(TutorialState state) =>
            new()
            {
                CompletedFlows = state.CompletedFlows.ToDictionary(
                    pair => pair.Key,
                    pair => Clone(pair.Value),
                    StringComparer.Ordinal),
                CompletedPackages = state.CompletedPackages.ToDictionary(
                    pair => pair.Key,
                    pair => Clone(pair.Value),
                    StringComparer.Ordinal)
            };

        private static TutorialCompletionRecord Clone(TutorialCompletionRecord record) =>
            new()
            {
                Version = record.Version,
                CompletionKind = record.CompletionKind,
                SourceFlowId = record.SourceFlowId,
                CompletedAt = record.CompletedAt
            };
    }
}
