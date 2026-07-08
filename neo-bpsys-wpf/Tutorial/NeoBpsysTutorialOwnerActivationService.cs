using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Determines active tutorial owners from the neo-bpsys main navigation and local tab state.
/// </summary>
public sealed class NeoBpsysTutorialOwnerActivationService(NavigationService navigationService)
    : ITutorialOwnerActivationService
{
    /// <inheritdoc />
    public bool IsOwnerActive(FrameworkElement owner, string pageKey)
    {
        if (owner.Dispatcher.HasShutdownStarted || owner.Dispatcher.HasShutdownFinished)
        {
            return false;
        }

        if (owner is Window window)
        {
            return window.IsVisible;
        }

        if (!owner.IsLoaded || !owner.IsVisible)
        {
            return false;
        }

        if (pageKey == FrontedWindowsView.TutorialPageKey
            || pageKey == FrontedLayoutPackagesView.TutorialPageKey)
        {
            return IsActiveFrontManageChild(owner, pageKey);
        }

        if (IsMainNavigationPageKey(pageKey))
        {
            return ReferenceEquals(navigationService.CurrentPageContent, owner)
                && navigationService.CurrentPageType == owner.GetType();
        }

        return Window.GetWindow(owner) is { IsVisible: true };
    }

    private bool IsActiveFrontManageChild(FrameworkElement owner, string pageKey)
    {
        if (navigationService.CurrentPageContent is not FrontManagePage frontManagePage
            || navigationService.CurrentPageType != typeof(FrontManagePage))
        {
            return false;
        }

        if (!frontManagePage.IsLoaded || !frontManagePage.IsVisible)
        {
            return false;
        }

        if (!FrontManagePage.TryResolveCurrentChildTutorial(frontManagePage.FrontManageTabs, out var activeOwner, out var activePageKey))
        {
            return false;
        }

        return ReferenceEquals(activeOwner, owner)
            && string.Equals(activePageKey, pageKey, StringComparison.Ordinal)
            && IsDescendantOf(owner, frontManagePage);
    }

    private static bool IsMainNavigationPageKey(string pageKey) =>
        pageKey == TutorialPageKeys.FrontManage
        || pageKey == TutorialPageKeys.SmartBp
        || pageKey == TutorialPageKeys.TeamInfo
        || pageKey == TutorialPageKeys.Score
        || pageKey == PickPage.TutorialPageKey
        || pageKey == BanSurPage.TutorialPageKey
        || pageKey == BanHunPage.TutorialPageKey
        || pageKey == TutorialPageKeys.BpGameGuidance
        || pageKey == TutorialPageKeys.GameManage;

    private static bool IsDescendantOf(DependencyObject candidate, DependencyObject ancestor)
    {
        var current = candidate;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current)
                ?? (current as FrameworkElement)?.Parent
                ?? (current as FrameworkContentElement)?.Parent;
        }

        return false;
    }
}
