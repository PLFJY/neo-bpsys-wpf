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
/// FrontedDesignerWindow 的Catalog业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private void RunUserSelection(Action selection)
    {
        _userSelectionDepth++;
        try
        {
            selection();
        }
        finally
        {
            _userSelectionDepth--;
        }
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _suppressSelectorReload)
        {
            return;
        }

        ScheduleSelectorReload();
    }

    private void ScheduleSelectorReload()
    {
        if (_selectorReloadInProgress)
        {
            _selectorReloadRequested = true;
            return;
        }

        if (_selectorReloadScheduled)
        {
            return;
        }

        _selectorReloadScheduled = true;
        Dispatcher.BeginInvoke(
            new Action(async () => await HandleScheduledSelectorReloadAsync()),
            DispatcherPriority.Background);
    }

    private async Task HandleScheduledSelectorReloadAsync()
    {
        _selectorReloadScheduled = false;
        if (_viewModel is null)
        {
            return;
        }

        if (_selectorReloadInProgress)
        {
            _selectorReloadRequested = true;
            return;
        }

        _selectorReloadInProgress = true;
        try
        {
            do
            {
                _selectorReloadRequested = false;
                if (ReferenceEquals(_lastAcceptedWindow, _viewModel.SelectedWindow))
                {
                    continue;
                }

                if (!await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
                {
                    RestoreAcceptedSelection();
                    return;
                }

                var loadingWindow = _viewModel.SelectedWindow;
                await _viewModel.ReloadLayoutCoreAsync();
                _lastAcceptedWindow = loadingWindow;
            }
            while (_selectorReloadRequested);
        }
        finally
        {
            _selectorReloadInProgress = false;
        }
    }

    private void RestoreAcceptedSelection()
    {
        if (_viewModel is null)
        {
            return;
        }

        _suppressSelectorReload = true;
        try
        {
            _viewModel.SelectedWindow = _lastAcceptedWindow;
        }
        finally
        {
            _suppressSelectorReload = false;
        }
    }

}
