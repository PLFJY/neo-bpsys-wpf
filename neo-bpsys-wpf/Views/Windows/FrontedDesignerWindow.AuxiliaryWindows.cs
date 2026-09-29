using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// FrontedDesignerWindow 的AuxiliaryWindows业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private void ValidationDetailsWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is ValidationDetailsWindow window && ReferenceEquals(window, _validationDetailsWindow))
        {
            window.Closed -= ValidationDetailsWindow_OnClosed;
            _validationDetailsWindow = null;
        }
    }

    private void CloseValidationDetailsWindowSafely()
    {
        var window = _validationDetailsWindow;
        if (window is null)
        {
            return;
        }

        window.Closed -= ValidationDetailsWindow_OnClosed;
        if (!window.IsVisible)
        {
            return;
        }

        try
        {
            window.Owner = null;
            window.Close();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Failed to close fronted designer validation details window safely.");
        }
    }

    private void OpenDesignerHelp_OnClick(object sender, RoutedEventArgs e)
    {
        if (_helpWindow is null || !_helpWindow.IsVisible)
        {
            _helpWindow = new FrontedDesignerHelpWindow
            {
                Owner = this
            };
            _helpWindow.Closed += HelpWindow_OnClosed;
            _helpWindow.Show();
            return;
        }

        _helpWindow.Activate();
    }

    private void HelpWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is FrontedDesignerHelpWindow window && ReferenceEquals(window, _helpWindow))
        {
            window.Closed -= HelpWindow_OnClosed;
            _helpWindow = null;
        }
    }

    private void CloseHelpWindowSafely()
    {
        var window = _helpWindow;
        if (window is null)
        {
            return;
        }

        window.Closed -= HelpWindow_OnClosed;
        if (!window.IsVisible)
        {
            return;
        }

        try
        {
            window.Owner = null;
            window.Close();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Failed to close fronted designer help window safely.");
        }
    }

}
