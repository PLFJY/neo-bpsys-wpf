using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Tutorial;
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
    public FrontManagePage()
    {
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
        Loaded += (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationFrontManageOpened);
            TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.FrontManage, "Loaded");
        };
        Loaded += OnLoaded;
        IsVisibleChanged += (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.FrontManage, "Visible");
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
            new Action(() => RunCurrentChildTutorial(FrontManageTabs)));
    }

    internal static bool RunCurrentChildTutorial(DependencyObject root)
    {
        if (!TryResolveCurrentChildTutorial(root, out var owner, out var pageKey))
        {
            return false;
        }

        TutorialPageLoader.RunPendingOnLoaded(owner, pageKey, "TabChanged");
        return true;
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
