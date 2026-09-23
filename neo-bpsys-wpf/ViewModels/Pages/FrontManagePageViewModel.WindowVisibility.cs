using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 前台管理页的WindowVisibility逻辑。
/// </summary>
public sealed partial class FrontManagePageViewModel
{
    [RelayCommand]
    private void ShowWindow(object? windowInfo)
    {
        switch (windowInfo)
        {
            case FrontedWindowType windowType:
                _frontedWindowService.ShowWindow(windowType);
                PublishBpWindowOpenedIfTarget(windowType);
                break;
            case string id:
                _frontedWindowService.ShowWindow(id);
                PublishBpWindowOpenedIfTarget(id);
                break;
        }
    }

    private static void PublishBpWindowOpenedIfTarget(FrontedWindowType windowType)
    {
        if (windowType is FrontedWindowType.BpWindow)
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.BpWindowOpened, new { Window = windowType.ToString() });
            ActivateMainWindow();
        }
    }

    private static void PublishBpWindowOpenedIfTarget(string windowId)
    {
        var bpWindowId = FrontedWindowHelper.GetFrontedWindowCanonicalId(FrontedWindowType.BpWindow);
        if (string.Equals(windowId, bpWindowId, StringComparison.Ordinal))
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.BpWindowOpened, new { WindowId = windowId });
            ActivateMainWindow();
        }
    }

    private static void ActivateMainWindow()
    {
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow is null)
        {
            return;
        }

        if (mainWindow.WindowState is WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }

    [RelayCommand]
    private void HideWindow(object? windowInfo)
    {
        switch (windowInfo)
        {
            case FrontedWindowType windowType:
                _frontedWindowService.HideWindow(windowType);
                break;
            case string id:
                _frontedWindowService.HideWindow(id);
                break;
        }
    }

    private static Window? GetShownOwnerWindow()
    {
        var current = Application.Current;
        if (current is null)
        {
            return null;
        }

        return current.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive && window.IsVisible)
               ?? (current.MainWindow?.IsVisible == true ? current.MainWindow : null)
               ?? current.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsVisible);
    }
}
