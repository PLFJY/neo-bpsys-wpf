using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Core;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// FrontManagePage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("0532C747-A9E8-44E7-A8FB-7416DF8FC4C6",
    "FrontendManagement",
    SymbolRegular.ShareScreenStart24,
    BackendPageCategory.External)]
public partial class FrontManagePage : Page, IRecipient<FrontManageTabNavigationMessage>
{
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;
    private CancellationTokenSource _tutorialLifetime = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FrontManagePage"/> class.
    /// </summary>
    /// <param name="tutorialRunner">Tutorial runner.</param>
    /// <param name="navigationService">Navigation service.</param>
    public FrontManagePage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();

        FrontManageTabs.MenuItems.Add(new NavigationViewItem(
            "FrontendWindows",
            SymbolRegular.ShareScreenStart24,
            typeof(FrontedWindowsView))
        {
            Name = "FrontedWindowsTab"
        });
        FrontManageTabs.MenuItems.Add(new NavigationViewItem(
            "LayoutPackages",
            SymbolRegular.AppsList24,
            typeof(FrontedLayoutPackagesView))
        {
            Name = "LayoutPackagesTab"
        });

        FrontManageTabs.Navigated += (_, _) => ScheduleCurrentChildTutorial();
        FrontManageTabs.SelectionChanged += (_, _) => ScheduleCurrentChildTutorial();
        Loaded += async (_, _) =>
        {
            if (_tutorialLifetime.IsCancellationRequested)
            {
                _tutorialLifetime.Dispose();
                _tutorialLifetime = new CancellationTokenSource();
            }

            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationFrontManageOpened);
            if (IsCurrentFrontManagePage())
            {
                await TryRunTutorialAsync();
            }
        };
        Unloaded += (_, _) => _tutorialLifetime.Cancel();
        Loaded += OnLoaded;
        IsVisibleChanged += async (_, e) =>
        {
            if (Equals(e.NewValue, true) && IsCurrentFrontManagePage())
            {
                await TryRunTutorialAsync();
            }
        };
        WeakReferenceMessenger.Default.Register(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FrontManageTabs.SelectFirstItemIfNoneSelected();
    }

    /// <inheritdoc/>
    public void Receive(FrontManageTabNavigationMessage message)
    {
        if (message.TabKey == FrontManageTabNavigationMessage.LayoutPackagesTabKey)
        {
            FrontManageTabs.Navigate(typeof(FrontedLayoutPackagesView));
            ScheduleCurrentChildTutorial();
        }
    }

    private void ScheduleCurrentChildTutorial()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(async () => await RunCurrentChildTutorialAsync()));
    }

    internal static bool RunCurrentChildTutorial(DependencyObject root)
    {
        if (IAppHost.Host is null)
        {
            return false;
        }

        if (!TryResolveCurrentChildTutorial(root, out var owner, out var pageKey))
        {
            return false;
        }

        var runner = IAppHost.Host.Services.GetService<ITutorialRunner>();
        _ = runner?.RunSequenceAsync(owner, pageKey, TutorialOwnerLifetime.GetToken(owner));
        return true;
    }

    private async Task RunCurrentChildTutorialAsync()
    {
        if (!IsCurrentFrontManagePage())
        {
            return;
        }

        if (!TryResolveCurrentChildTutorial(FrontManageTabs, out var owner, out var pageKey))
        {
            return;
        }

        var runner = _tutorialRunner ?? IAppHost.Host?.Services.GetService<ITutorialRunner>();
        if (runner == null)
        {
            return;
        }

        var token = owner switch
        {
            FrontedWindowsView windowsView => windowsView.TutorialLifetimeToken,
            FrontedLayoutPackagesView packagesView => packagesView.TutorialLifetimeToken,
            _ => _tutorialLifetime.Token
        };
        await runner.RunSequenceAsync(owner, pageKey, token);
    }

    private async Task TryRunTutorialAsync()
    {
        var runner = _tutorialRunner ?? IAppHost.Host?.Services.GetService<ITutorialRunner>();
        if (runner == null)
        {
            return;
        }

        var result = await runner.RunSequenceAsync(this, TutorialPageKeys.FrontManage, _tutorialLifetime.Token);
        if (result is TutorialRunResult.Completed or TutorialRunResult.NotPending)
        {
            await RunCurrentChildTutorialAsync();
        }
    }

    private bool IsCurrentFrontManagePage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
    }

    internal static bool TryResolveCurrentChildTutorial(
        DependencyObject root,
        out FrameworkElement owner,
        out string pageKey)
    {
        if (TryFindVisibleDescendant<FrontedWindowsView>(root, out var frontedWindowsView))
        {
            owner = frontedWindowsView;
            pageKey = FrontedWindowsView.TutorialPageKey;
            return true;
        }

        if (TryFindVisibleDescendant<FrontedLayoutPackagesView>(root, out var layoutPackagesView))
        {
            owner = layoutPackagesView;
            pageKey = FrontedLayoutPackagesView.TutorialPageKey;
            return true;
        }

        owner = null!;
        pageKey = string.Empty;
        return false;
    }

    private static bool TryFindVisibleDescendant<TView>(DependencyObject root, out TView view)
        where TView : FrameworkElement
    {
        if (root is TView typed && typed.IsVisible)
        {
            view = typed;
            return true;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            if (TryFindVisibleDescendant(VisualTreeHelper.GetChild(root, i), out view))
            {
                return true;
            }
        }

        if (root is ContentControl { Content: DependencyObject content }
            && TryFindVisibleDescendant(content, out view))
        {
            return true;
        }

        view = null!;
        return false;
    }
}
