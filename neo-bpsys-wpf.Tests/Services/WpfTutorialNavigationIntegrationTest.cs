#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Controls.Modern.Navigation;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Providers;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

[Collection(WpfUiCollectionDefinition.Name)]
public sealed class WpfTutorialNavigationIntegrationTest
{
    [Fact]
    public async Task FrontManageNavigation_ShouldTriggerOverviewAndChildViewTutorials()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var observer = new RecordingTutorialRunObserver();
            await using var app = await RealAppTestHost.StartAsync(observer);
            var hostWindow = app.HostWindow;

            var navigation = app.Navigation;
            Assert.True(NavigateIgnoringClosedLocalizationNotifications(
                () => navigation.Navigate(typeof(FrontManagePage))));
            await WaitForDispatcherAsync(hostWindow);

            await observer.WaitForAutoRunAsync("FrontManagePage", TutorialPageKeys.FrontManage, app.Dump);
            await observer.WaitForStartedAsync(TutorialPackageIds.FrontManageOverview, app.Dump);
            await CompletePackageAsync(hostWindow, observer, TutorialPackageIds.FrontManageOverview, app.Dump);

            var page = Assert.IsType<FrontManagePage>(navigation.CurrentContent);
            Assert.True(FrontManagePage.RunCurrentChildTutorial(page.FrontManageTabs));
            await observer.WaitForAutoRunAsync("FrontedWindowsView", FrontedWindowsView.TutorialPageKey, app.Dump);
            await observer.WaitForStartedAsync(TutorialPackageIds.FrontManageWindowsBasic, app.Dump);
            await CompletePackageAsync(hostWindow, observer, TutorialPackageIds.FrontManageWindowsBasic, app.Dump);

            NavigateIgnoringClosedLocalizationNotifications(
                () => page.FrontManageTabs.Navigate(typeof(FrontedWindowsView)));
            await WaitForDispatcherAsync(hostWindow);
            var nextWindowsPackage = await observer.WaitForAnyStartedAsync(
                [TutorialPackageIds.FrontManageOpenDesigner, TutorialPackageIds.FrontManageBpWindowLaunchBasic],
                app.Dump);
            if (nextWindowsPackage == TutorialPackageIds.FrontManageOpenDesigner)
            {
                TutorialSignalPublisher.Publish(TutorialSignalIds.DesignerV3Opened);
            }
            else
            {
                TutorialSignalPublisher.Publish(TutorialSignalIds.BpWindowOpened);
            }

            await CompletePackageAsync(hostWindow, observer, nextWindowsPackage, app.Dump);
            await CompleteIfAlreadyStartedAsync(
                hostWindow,
                observer,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialSignalIds.BpWindowOpened,
                app.Dump);

            NavigateIgnoringClosedLocalizationNotifications(
                () => page.FrontManageTabs.Navigate(typeof(FrontedLayoutPackagesView)));
            await WaitForDispatcherAsync(hostWindow);
            await observer.WaitForAutoRunAsync("FrontedLayoutPackagesView", FrontedLayoutPackagesView.TutorialPageKey, app.Dump);
            await CompleteIfAlreadyStartedAsync(
                hostWindow,
                observer,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialSignalIds.BpWindowOpened,
                app.Dump);
            Assert.True(FrontManagePage.RunCurrentChildTutorial(page.FrontManageTabs));
            await observer.WaitForStartedAsync(TutorialPackageIds.FrontManageLayoutPackagesBasic, app.Dump);
            await CompletePackageAsync(hostWindow, observer, TutorialPackageIds.FrontManageLayoutPackagesBasic, app.Dump);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task SmartBpNavigation_ShouldTriggerModuleShellAndLoadedModuleTutorials()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var observer = new RecordingTutorialRunObserver();
            await using var app = await RealAppTestHost.StartAsync(observer);
            var hostWindow = app.HostWindow;

            var navigation = app.Navigation;
            Assert.True(NavigateIgnoringClosedLocalizationNotifications(
                () => navigation.Navigate(typeof(SmartBpPage))));
            await WaitForDispatcherAsync(hostWindow);

            await observer.WaitForAutoRunAsync("SmartBpPage", TutorialPageKeys.SmartBp, app.Dump);
            await observer.WaitForStartedAsync(TutorialPackageIds.SmartBpModuleShell, app.Dump);
            await CompletePackageAsync(hostWindow, observer, TutorialPackageIds.SmartBpModuleShell, app.Dump);

            var page = Assert.IsType<SmartBpPage>(navigation.CurrentContent);
            var viewModel = Assert.IsType<SmartBpPageViewModel>(page.DataContext);
            viewModel.ModuleContent = CreateSmartBpModuleContent();
            viewModel.IsModuleLoaded = true;
            await WaitForDispatcherAsync(page);

            await observer.WaitForAutoRunAsync("SmartBpPage", TutorialPageKeys.SmartBp, app.Dump);
            await observer.WaitForStartedAsync(TutorialPackageIds.SmartBpModuleContentOverview, app.Dump);
            await CompletePackageAsync(hostWindow, observer, TutorialPackageIds.SmartBpModuleContentOverview, app.Dump);

            TutorialPageLoader.RunPendingOnLoaded(page, TutorialPageKeys.SmartBp);
            await observer.WaitForStartedAsync(TutorialPackageIds.SmartBpCaptureBasic, app.Dump);
            await CompletePackageAsync(hostWindow, observer, TutorialPackageIds.SmartBpCaptureBasic, app.Dump);

            TutorialPageLoader.RunPendingOnLoaded(page, TutorialPageKeys.SmartBp);
            await observer.WaitForStartedAsync(TutorialPackageIds.SmartBpRegionEditorBasic, app.Dump);
            await CompletePackageAsync(hostWindow, observer, TutorialPackageIds.SmartBpRegionEditorBasic, app.Dump);

            TutorialPageLoader.RunPendingOnLoaded(page, TutorialPageKeys.SmartBp);
            await observer.WaitForStartedAsync(TutorialPackageIds.SmartBpFullBpFlowBasic, app.Dump);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task WpfIntegrationTests_ShouldCloseWindowsWithoutDirtyPrompt()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var observer = new RecordingTutorialRunObserver();
            var app = await RealAppTestHost.StartAsync(observer);
            await app.DisposeAsync();
            Assert.False(app.HostWindow.IsVisible);
        }, TimeSpan.FromSeconds(10));
    }

    private static Grid CreateSmartBpModuleContent() =>
        new()
        {
            Children =
            {
                new ComboBox { Name = TutorialTargetNames.SmartBpWindowSelector },
                new Button { Name = TutorialTargetNames.SmartBpStartCaptureButton, Content = "Capture" },
                new Border { Name = TutorialTargetNames.SmartBpPreviewPanel, Width = 80, Height = 40 },
                new Button { Name = TutorialTargetNames.SmartBpStopCaptureButton, Content = "Stop" },
                new Button { Name = TutorialTargetNames.SmartBpRegionEditorButton, Content = "Region" },
                new Border { Name = TutorialTargetNames.SmartBpRegionPreviewPanel, Width = 80, Height = 40 },
                new StackPanel { Name = TutorialTargetNames.SmartBpRegionListPanel },
                new Button { Name = TutorialTargetNames.SmartBpSaveRegionButton, Content = "Save" },
                new Button { Name = TutorialTargetNames.SmartBpStartFullBpFlowButton, Content = "Full" }
            }
        };

    private static async Task CompleteVisibleTutorialsAsync(DependencyObject root)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            var overlay = FindActiveOverlay(root);
            if (overlay is null)
            {
                await Task.Delay(80);
                if (FindVisualChildren<ProductTourOverlay>(root).FirstOrDefault() is null)
                {
                    return;
                }

                continue;
            }

            var button = FindButtonByContent(overlay, "下一步")
                ?? FindButtonByContent(overlay, "完成")
                ?? FindButtonByContent(overlay, "继续");
            Assert.NotNull(button);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await overlay.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await Task.Delay(40);
        }

        throw new TimeoutException("Timed out while completing visible product tour overlays.");
    }

    private static async Task CompletePackageAsync(
        DependencyObject root,
        RecordingTutorialRunObserver observer,
        string packageId,
        Func<string> dump)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            if (observer.CompletedPackageIds.Contains(packageId))
            {
                return;
            }

            if (observer.PackageResults.TryGetValue(packageId, out var result)
                && result == TutorialRunResult.Completed)
            {
                return;
            }

            var overlay = FindVisualChildren<ProductTourOverlay>(root).FirstOrDefault();
            if (overlay is null)
            {
                await Task.Delay(50);
                continue;
            }

            var button = FindButtonByContent(overlay, "下一步")
                ?? FindButtonByContent(overlay, "完成")
                ?? FindButtonByContent(overlay, "继续");
            Assert.NotNull(button);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await overlay.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await Task.Delay(40);
        }

        Assert.Fail($"Timed out completing tutorial package: {packageId}\n{dump()}");
    }

    private static async Task CompleteIfAlreadyStartedAsync(
        DependencyObject root,
        RecordingTutorialRunObserver observer,
        string packageId,
        string signalId,
        Func<string> dump)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (observer.CompletedPackageIds.Contains(packageId))
            {
                return;
            }

            if (observer.StartedPackageIds.Contains(packageId))
            {
                TutorialSignalPublisher.Publish(signalId);
                await CompletePackageAsync(root, observer, packageId, dump);
                return;
            }

            await Task.Delay(50);
        }
    }

    private static ProductTourOverlay? FindActiveOverlay(DependencyObject root)
    {
        var overlay = FindVisualChildren<ProductTourOverlay>(root).FirstOrDefault();
        if (overlay is not null)
        {
            return overlay;
        }

        if (Application.Current is null)
        {
            return null;
        }

        return Application.Current.Windows
            .OfType<Window>()
            .SelectMany(FindVisualChildren<ProductTourOverlay>)
            .FirstOrDefault();
    }

    private static Button? FindButtonByContent(DependencyObject root, string content)
    {
        if (root is Button button && Equals(button.Content, content))
        {
            return button;
        }

        foreach (var child in GetVisualChildren(root))
        {
            var result = FindButtonByContent(child, content);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T typed)
        {
            yield return typed;
        }

        foreach (var child in GetVisualChildren(root))
        {
            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<DependencyObject> GetVisualChildren(DependencyObject root)
    {
        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            yield return System.Windows.Media.VisualTreeHelper.GetChild(root, index);
        }

        if (root is ContentControl { Content: DependencyObject content })
        {
            yield return content;
        }
    }

    private static async Task WaitForDispatcherAsync(DispatcherObject dispatcherObject)
    {
        await dispatcherObject.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await dispatcherObject.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static bool NavigateIgnoringClosedLocalizationNotifications(Func<bool> navigate)
    {
        try
        {
            return navigate();
        }
        catch (AggregateException ex) when (RealAppTestHost.IsClosedDispatcherLocalizationException(ex))
        {
            return true;
        }
    }

    private static void NavigateIgnoringClosedLocalizationNotifications(Action navigate)
    {
        try
        {
            navigate();
        }
        catch (AggregateException ex) when (RealAppTestHost.IsClosedDispatcherLocalizationException(ex))
        {
        }
    }

    private sealed class RealAppTestHost : IAsyncDisposable
    {
        private readonly IHost? _previousHost;
        private readonly IHost _host;
        private readonly RecordingTutorialRunObserver _observer;
        private readonly DispatcherUnhandledExceptionEventHandler _dispatcherExceptionHandler;

        private RealAppTestHost(
            IHost? previousHost,
            IHost host,
            RecordingTutorialRunObserver observer,
            Window hostWindow,
            ModernNavigationView navigation,
            DispatcherUnhandledExceptionEventHandler dispatcherExceptionHandler)
        {
            _previousHost = previousHost;
            _host = host;
            _observer = observer;
            _dispatcherExceptionHandler = dispatcherExceptionHandler;
            HostWindow = hostWindow;
            Navigation = navigation;
        }

        public Window HostWindow { get; }

        public ModernNavigationView Navigation { get; }

        public static async Task<RealAppTestHost> StartAsync(RecordingTutorialRunObserver observer)
        {
            var previousHost = IAppHost.Host;
            BackendPagesRegistryService.Registered.Clear();
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<ITutorialRunObserver>(observer);
                    var configure = typeof(App).GetMethod(
                        "ConfigureServices",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    Assert.NotNull(configure);
                    configure.Invoke(null, [context, services]);
                    services.RemoveAll<ITutorialStateStore>();
                    services.AddSingleton<ITutorialStateStore, InMemoryTutorialStateStore>();
                    services.RemoveAll<ITutorialAvatarProvider>();
                    services.AddSingleton<ITutorialAvatarProvider, NoOpTutorialAvatarProvider>();
                })
                .Build();

            IAppHost.Host = host;
            host.Services.GetRequiredService<ProductTourRegistrationMarker>();
            DispatcherUnhandledExceptionEventHandler dispatcherExceptionHandler = (_, args) =>
            {
                if (IsClosedDispatcherLocalizationException(args.Exception))
                {
                    args.Handled = true;
                }
            };
            Dispatcher.CurrentDispatcher.UnhandledException += dispatcherExceptionHandler;
            var hostWindow = new Window
            {
                Width = 1000,
                Height = 720,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };
            EnsureLocalizationInitialized(hostWindow);
            var navigation = new ModernNavigationView
            {
                TransitionDuration = 0,
                MenuItemsSource = new[]
                {
                    new Wpf.Ui.Controls.NavigationViewItem(
                        "FrontendManagement",
                        Wpf.Ui.Controls.SymbolRegular.ShareScreenStart24,
                        typeof(FrontManagePage)),
                    new Wpf.Ui.Controls.NavigationViewItem(
                        "SmartBp",
                        Wpf.Ui.Controls.SymbolRegular.ScanText24,
                        typeof(SmartBpPage))
                }
            };
            navigation.SetServiceProvider(host.Services);
            navigation.SetPageProviderService(host.Services.GetRequiredService<INavigationViewPageProvider>());
            host.Services.GetRequiredService<INavigationService>().SetNavigationControl(navigation);
            hostWindow.Content = navigation;
            hostWindow.Show();
            await WaitForDispatcherAsync(hostWindow);
            return new RealAppTestHost(previousHost, host, observer, hostWindow, navigation, dispatcherExceptionHandler);
        }

        private static void EnsureLocalizationInitialized(DependencyObject root)
        {
            var culture = CultureInfo.GetCultureInfo("zh-CN");
            if (!string.Equals(
                    ResxLocalizationProvider.GetDefaultAssembly(root),
                    "neo-bpsys-wpf",
                    StringComparison.Ordinal))
            {
                IgnoreClosedDispatcherLocalizationNotifications(
                    () => ResxLocalizationProvider.SetDefaultAssembly(root, "neo-bpsys-wpf"));
            }

            if (!string.Equals(
                    ResxLocalizationProvider.GetDefaultDictionary(root),
                    "Locales.Lang",
                    StringComparison.Ordinal))
            {
                IgnoreClosedDispatcherLocalizationNotifications(
                    () => ResxLocalizationProvider.SetDefaultDictionary(root, "Locales.Lang"));
            }

            if (!Equals(LocalizeDictionary.Instance.Culture, culture))
            {
                IgnoreClosedDispatcherLocalizationNotifications(
                    () => LocalizeDictionary.Instance.Culture = culture);
            }

        }

        private static void IgnoreClosedDispatcherLocalizationNotifications(Action action)
        {
            try
            {
                action();
            }
            catch (AggregateException ex) when (IsClosedDispatcherLocalizationException(ex))
            {
            }
        }

        internal static bool IsClosedDispatcherLocalizationException(Exception exception) =>
            exception is TaskCanceledException
            || exception is AggregateException aggregate
            && aggregate.InnerExceptions.All(IsClosedDispatcherLocalizationException);

        public string Dump()
        {
            var frontManagePage = Navigation.CurrentContent as FrontManagePage;
            var builder = new StringBuilder();
            builder.AppendLine("Observed packages:");
            foreach (var packageId in _observer.StartedPackageIds)
            {
                builder.AppendLine(packageId);
            }

            builder.AppendLine($"Observed auto-run requests: {string.Join(", ", _observer.AutoRunRequests)}");
            builder.AppendLine($"Observed sequences: {string.Join(", ", _observer.SequenceResolutions)}");
            builder.AppendLine($"State skipped packages: {string.Join(", ", _observer.StateSkippedPackages)}");
            builder.AppendLine($"CanRun skipped packages: {string.Join(", ", _observer.CanRunSkippedPackages)}");
            builder.AppendLine($"Current navigation content: {Navigation.CurrentContent?.GetType().FullName ?? "<null>"}");
            builder.AppendLine($"Visible FrontManage child view: {DescribeFrontManageChild(frontManagePage)}");
            builder.AppendLine($"FrontManageTabs selected item: {frontManagePage?.FrontManageTabs.SelectedItem}");
            builder.AppendLine($"Completed packages: {string.Join(", ", _observer.CompletedPackageIds)}");
            builder.AppendLine($"Package results: {string.Join(", ", _observer.PackageResults.Select(pair => $"{pair.Key}={pair.Value}"))}");
            builder.AppendLine($"Shown steps: {string.Join(", ", _observer.ShownSteps)}");
            builder.AppendLine($"Target missing packages: {string.Join(", ", _observer.TargetMissingPackageIds)}");
            builder.AppendLine($"Overlay count: {FindVisualChildren<ProductTourOverlay>(HostWindow).Count()}");
            builder.AppendLine($"Last result: {_observer.LastResult?.ToString() ?? "<none>"}");
            return builder.ToString();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                HostWindow.Close();
            }
            finally
            {
                HostWindow.Dispatcher.UnhandledException -= _dispatcherExceptionHandler;
                IAppHost.Host = _previousHost;
                _host.Dispose();
            }

            await Task.CompletedTask;
        }

        private static string DescribeFrontManageChild(FrontManagePage? page)
        {
            if (page is null)
            {
                return "<none>";
            }

            if (FrontManagePage.TryResolveCurrentChildTutorial(page.FrontManageTabs, out var owner, out _))
            {
                return owner.GetType().FullName ?? owner.GetType().Name;
            }

            return "<none>";
        }
    }

    private sealed class RecordingTutorialRunObserver : ITutorialRunObserver
    {
        private readonly ConcurrentQueue<string> _startedPackageIds = [];
        private readonly ConcurrentQueue<string> _completedPackageIds = [];
        private readonly ConcurrentQueue<string> _shownSteps = [];
        private readonly ConcurrentQueue<string> _targetMissingPackageIds = [];
        private readonly ConcurrentQueue<string> _autoRunRequests = [];
        private readonly ConcurrentQueue<string> _sequenceResolutions = [];
        private readonly ConcurrentQueue<string> _stateSkippedPackages = [];
        private readonly ConcurrentQueue<string> _canRunSkippedPackages = [];
        private readonly ConcurrentDictionary<string, TutorialRunResult> _packageResults = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> StartedPackageIds => _startedPackageIds.ToArray();

        public IReadOnlyCollection<string> CompletedPackageIds => _completedPackageIds.ToArray();

        public IReadOnlyCollection<string> ShownSteps => _shownSteps.ToArray();

        public IReadOnlyCollection<string> TargetMissingPackageIds => _targetMissingPackageIds.ToArray();

        public IReadOnlyCollection<string> AutoRunRequests => _autoRunRequests.ToArray();

        public IReadOnlyCollection<string> SequenceResolutions => _sequenceResolutions.ToArray();

        public IReadOnlyCollection<string> StateSkippedPackages => _stateSkippedPackages.ToArray();

        public IReadOnlyCollection<string> CanRunSkippedPackages => _canRunSkippedPackages.ToArray();

        public IReadOnlyDictionary<string, TutorialRunResult> PackageResults => _packageResults;

        public TutorialRunResult? LastResult { get; private set; }

        public void OnAutoRunRequested(string ownerType, string pageKey, string reason)
        {
            _autoRunRequests.Enqueue($"{ownerType}:{pageKey}:{reason}");
        }

        public void OnAutoRunCompleted(string ownerType, string pageKey, TutorialRunResult result)
        {
        }

        public void OnPackageRunRequested(string packageId, string pageKey, TutorialTriggerMode triggerMode)
        {
        }

        public void OnPackageStarted(string packageId, string pageKey, TutorialTriggerMode triggerMode)
        {
            _startedPackageIds.Enqueue(packageId);
        }

        public void OnStepShown(string packageId, string? targetName, string title)
        {
            _shownSteps.Enqueue($"{packageId}:{targetName ?? "<center>"}:{title}");
        }

        public void OnPackageCompleted(string packageId, TutorialRunResult result)
        {
            _completedPackageIds.Enqueue(packageId);
            _packageResults[packageId] = result;
            LastResult = result;
        }

        public void OnPackageNotPending(string pageKey)
        {
        }

        public void OnPackageSkippedByState(
            string packageId,
            TutorialCompletionKind completionKind,
            int recordedVersion,
            int currentVersion)
        {
            _stateSkippedPackages.Enqueue($"{packageId}:{completionKind}:{recordedVersion}->{currentVersion}");
        }

        public void OnPackageSkippedByCanRun(string packageId, string pageKey)
        {
            _canRunSkippedPackages.Enqueue($"{pageKey}:{packageId}");
        }

        public void OnSequenceResolved(
            string pageKey,
            IReadOnlyList<string> packageIds,
            TutorialAutoRunStrategy strategy)
        {
            _sequenceResolutions.Enqueue($"{pageKey}:{strategy}:[{string.Join("|", packageIds)}]");
        }

        public void OnPackageSuppressed(string pageKey)
        {
        }

        public void OnPackageTargetMissing(string packageId)
        {
            _targetMissingPackageIds.Enqueue(packageId);
        }

        public async Task WaitForStartedAsync(string packageId, Func<string> dump)
        {
            await WaitForAnyStartedAsync([packageId], dump);
        }

        public async Task WaitForAutoRunAsync(string ownerType, string pageKey, Func<string> dump)
        {
            var expected = $"{ownerType}:{pageKey}:";
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                if (_autoRunRequests.Any(request => request.StartsWith(expected, StringComparison.Ordinal)))
                {
                    return;
                }

                await Task.Delay(50);
            }

            Assert.Fail($"Timed out waiting for auto-run: {ownerType} {pageKey}\n{dump()}");
        }

        public async Task<string> WaitForAnyStartedAsync(IReadOnlyCollection<string> packageIds, Func<string> dump)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                var started = _startedPackageIds.FirstOrDefault(packageIds.Contains);
                if (started is not null)
                {
                    return started;
                }

                await Task.Delay(50);
            }

            Assert.Fail($"Timed out waiting for tutorial package: {string.Join(" or ", packageIds)}\n{dump()}");
            return string.Empty;
        }
    }

    private sealed class InMemoryTutorialStateStore : ITutorialStateStore
    {
        private TutorialState _state = new();

        public Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(Clone(_state));
        }

        public Task SaveAsync(TutorialState state, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _state = Clone(state);
            return Task.CompletedTask;
        }

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _state = new TutorialState();
            return Task.CompletedTask;
        }

        private static TutorialState Clone(TutorialState source) =>
            new()
            {
                CompletedFlows = source.CompletedFlows.ToDictionary(
                    pair => pair.Key,
                    pair => new TutorialCompletionRecord
                    {
                        CompletedAt = pair.Value.CompletedAt,
                        CompletionKind = pair.Value.CompletionKind,
                        SourceFlowId = pair.Value.SourceFlowId,
                        Version = pair.Value.Version
                    }),
                CompletedPackages = source.CompletedPackages.ToDictionary(
                    pair => pair.Key,
                    pair => new TutorialCompletionRecord
                    {
                        CompletedAt = pair.Value.CompletedAt,
                        CompletionKind = pair.Value.CompletionKind,
                        SourceFlowId = pair.Value.SourceFlowId,
                        Version = pair.Value.Version
                    })
            };
    }
}
