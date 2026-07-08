using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Controls.Modern.Navigation;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tests.Infrastructure;
using Wpf.Ui.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests Product Tour target element resolution.
/// </summary>
public sealed class TargetElementFinderTest
{
    [Fact]
    public async Task NavigationItemFinderFindsGeneratedModernNavigationButtonByTargetPageType()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var navigationView = new ModernNavigationView
            {
                MenuItemsSource = new[]
                {
                    new NavigationViewItem("TeamInfo", SymbolRegular.PeopleTeam24, typeof(TestPage))
                }
            };
            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.UpdateLayout();

                var result = await TargetElementFinder.FindNavigationItemAsync(
                    navigationView,
                    typeof(TestPage).FullName!,
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None);

                Assert.NotNull(result);
                Assert.IsType<System.Windows.Controls.Button>(result);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task NavigationItemFinderFindsGeneratedModernNavigationButtonByTag()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var navigationView = new ModernNavigationView
            {
                MenuItemsSource = new[]
                {
                    new NavigationViewItem("TeamInfo", SymbolRegular.PeopleTeam24, typeof(TestPage))
                    {
                        TargetPageTag = "Navigation.TeamInfo"
                    }
                }
            };
            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.UpdateLayout();

                var result = await TargetElementFinder.FindNavigationItemAsync(
                    navigationView,
                    "Navigation.TeamInfo",
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None);

                Assert.NotNull(result);
                Assert.IsType<System.Windows.Controls.Button>(result);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task NavigationItemTargetMissingReturnsTargetMissingAndLogsKindAndKey()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var logger = new ListLogger<TutorialService>();
            var packageRegistry = new TutorialPackageRegistry();
            var flowRegistry = new TutorialFlowRegistry();
            var service = new TutorialService(
                new EmptyServiceProvider(),
                packageRegistry,
                new TutorialSequenceRegistry(),
                flowRegistry,
                new InMemoryTutorialStateStore(),
                new TutorialSignalService(),
                new DefaultTutorialTextProvider(),
                new NoOpTutorialAvatarProvider(),
                new ProductTourOptions(),
                logger);
            packageRegistry.Register(new TutorialPackageDefinition
            {
                PackageId = "Package.Navigation.Missing",
                PageKey = "Page.Test",
                Steps =
                [
                    new ProductTourStep
                    {
                        TargetKind = TutorialTargetKind.NavigationItem,
                        TargetKey = "Missing.Page.Type",
                        Title = "Missing",
                        Description = "Missing navigation target",
                        Timeout = TimeSpan.FromMilliseconds(20)
                    }
                ]
            });
            flowRegistry.Register(new TutorialFlowDefinition
            {
                FlowId = "Flow.Navigation.Missing",
                Items = [new PackageFlowItem { PackageId = "Package.Navigation.Missing" }]
            });

            var result = await service.RunFlowAsync(new System.Windows.Controls.Grid(), "Flow.Navigation.Missing");

            Assert.Equal(TutorialRunResult.TargetMissing, result);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == LogLevel.Warning);
            Assert.Contains("TargetKind=NavigationItem", warning.Message);
            Assert.Contains("TargetKey=Missing.Page.Type", warning.Message);
        });
    }

    [Fact]
    public async Task DescendantTypeFinderFindsFirstMatchingElementUnderHost()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var target = new DescendantTargetControl();
            var host = new System.Windows.Controls.StackPanel
            {
                Name = "HostPanel",
                Children =
                {
                    new System.Windows.Controls.TextBlock(),
                    target
                }
            };
            var root = new System.Windows.Controls.Grid();
            root.Children.Add(host);
            var window = CreateHiddenWindow(root);
            try
            {
                window.Show();
                window.UpdateLayout();

                var result = await TargetElementFinder.FindDescendantTypeAsync(
                    root,
                    "HostPanel",
                    typeof(DescendantTargetControl).FullName!,
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None);

                Assert.Same(target, result);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task DescendantTypeFinderReturnsNullWhenTargetIsMissing()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var root = new System.Windows.Controls.Grid
            {
                Children =
                {
                    new System.Windows.Controls.StackPanel { Name = "HostPanel" }
                }
            };

            var result = await TargetElementFinder.FindDescendantTypeAsync(
                root,
                "HostPanel",
                typeof(DescendantTargetControl).FullName!,
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None);

            Assert.Null(result);
        });
    }

    [Fact]
    public async Task ElementTagFinderFindsTaggedElement()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var target = new System.Windows.Controls.Button { Tag = "BpWindowId" };
            var root = new System.Windows.Controls.Grid
            {
                Children =
                {
                    new System.Windows.Controls.Button { Tag = "OtherWindowId" },
                    target
                }
            };
            var window = CreateHiddenWindow(root);
            try
            {
                window.Show();
                window.UpdateLayout();

                var result = await TargetElementFinder.FindByElementTagAsync(
                    root,
                    "BpWindowId",
                    TimeSpan.FromSeconds(2),
                    CancellationToken.None);

                Assert.Same(target, result);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ElementTagFinderReturnsNullWhenTargetIsMissing()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var root = new System.Windows.Controls.Grid
            {
                Children =
                {
                    new System.Windows.Controls.Button { Tag = "OtherWindowId" }
                }
            };

            var result = await TargetElementFinder.FindByElementTagAsync(
                root,
                "BpWindowId",
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None);

            Assert.Null(result);
        });
    }

    private static Window CreateHiddenWindow(UIElement content) =>
        new()
        {
            Width = 420,
            Height = 160,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = content
        };

    private sealed class TestPage : System.Windows.Controls.Page
    {
    }

    private sealed class DescendantTargetControl : System.Windows.Controls.Control
    {
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class InMemoryTutorialStateStore : ITutorialStateStore
    {
        public Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TutorialState());

        public Task SaveAsync(TutorialState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResetAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state as IEnumerable<KeyValuePair<string, object?>>;
            var message = properties == null
                ? formatter(state, exception)
                : string.Join(", ", properties.Select(pair => $"{pair.Key}={pair.Value}"));
            Entries.Add(new LogEntry(logLevel, message));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);
}
