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
/// FrontedDesignerWindow 的Commands业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        UpdateShiftSnapState();

        if (_viewModel is null || ShouldIgnoreKeyboardInput())
        {
            return;
        }

        var isControl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (!isControl)
        {
            return;
        }

        if (e.Key == Key.S && !isShift)
        {
            if (_viewModel.CanSaveLayout)
            {
                _viewModel.SaveLayoutCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Z && !isShift)
        {
            _viewModel.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Y || (e.Key == Key.Z && isShift))
        {
            _viewModel.RedoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.C && !isShift)
        {
            _viewModel.CopySelectedControlCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.V && !isShift)
        {
            _viewModel.PasteControlCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Window_OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        UpdateShiftSnapState();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _viewModel?.UpdateShiftSnapActive(false);
        _viewModel?.ClearActiveSnapGuides();
    }

    private void AddControlButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (AddControlButton.ContextMenu is null)
        {
            return;
        }

        RebuildAddControlContextMenu();
        AddControlButton.ContextMenu.PlacementTarget = AddControlButton;
        AddControlButton.ContextMenu.IsOpen = true;
    }

    private void RebuildAddControlContextMenu()
    {
        if (_viewModel is null || AddControlButton.ContextMenu is null)
        {
            return;
        }

        AddControlButton.ContextMenu.Items.Clear();
        foreach (var group in _viewModel.AddControlCatalogGroups)
        {
            if (AddControlButton.ContextMenu.Items.Count > 0)
            {
                AddControlButton.ContextMenu.Items.Add(new Separator());
            }

            AddControlButton.ContextMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = group.DisplayName,
                IsEnabled = false
            });

            foreach (var item in group.Items)
            {
                var menuItem = new System.Windows.Controls.MenuItem
                {
                    Header = item.DisplayName,
                    Tag = item.ControlType,
                    ToolTip = string.IsNullOrWhiteSpace(item.Description) ? item.ControlType : item.Description,
                    IsEnabled = item.IsAvailable
                };
                menuItem.Click += AddControlMenuItem_OnClick;
                AddControlButton.ContextMenu.Items.Add(menuItem);
            }
        }
    }

    private void AddControlMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not System.Windows.Controls.MenuItem { Tag: string controlType })
        {
            return;
        }

        _viewModel.AddControlCommand.Execute(new FrontedAddControlRequest
        {
            ControlType = controlType,
            CenterX = GetViewportCenterX(),
            CenterY = GetViewportCenterY()
        });
        FocusDesignSurface();
    }

    private async void ReloadLayoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
        {
            return;
        }

        await _viewModel.ReloadLayoutCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
    }

    private async void SaveLayoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var saved = await _viewModel.SaveCurrentLayoutAsync();
        if (!saved && _viewModel.ErrorCount > 0)
        {
            OpenValidationDetails_OnClick(sender, e);
        }
    }

    private async void ResetToBuiltInButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
        {
            return;
        }

        if (!await ConfirmResetToBuiltInAsync())
        {
            return;
        }

        await _viewModel.ResetToBuiltInCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceCloseAfterDirtyPrompt || _viewModel?.HasUnsavedChanges != true)
        {
            return;
        }

        e.Cancel = true;
        if (_isDirtyClosePromptOpen)
        {
            return;
        }

        _isDirtyClosePromptOpen = true;
        Dispatcher.BeginInvoke(
            new Action(async () => await PromptDirtyCloseAfterCancelAsync()),
            DispatcherPriority.Background);
    }

    private async Task PromptDirtyCloseAfterCancelAsync()
    {
        try
        {
            var result = await ShowDirtyPromptAsync("SaveBeforeClose");
            if (result == MessageBoxResult.Primary)
            {
                if (_viewModel is not null && await _viewModel.SaveCurrentLayoutAsync())
                {
                    _forceCloseAfterDirtyPrompt = true;
                    Close();
                }
            }
            else if (result == MessageBoxResult.Secondary)
            {
                _viewModel?.DiscardPendingResourceImports();
                _forceCloseAfterDirtyPrompt = true;
                Close();
            }
        }
        finally
        {
            _isDirtyClosePromptOpen = false;
        }
    }

    private async Task<bool> ConfirmDirtyDocumentCanContinueAsync(string messageKey)
    {
        if (_viewModel?.HasUnsavedChanges != true)
        {
            return true;
        }

        var result = await ShowDirtyPromptAsync(messageKey);
        if (result == MessageBoxResult.Primary)
        {
            return await _viewModel.SaveCurrentLayoutAsync();
        }

        if (result == MessageBoxResult.Secondary)
        {
            _viewModel.DiscardPendingResourceImports();
        }

        return result == MessageBoxResult.Secondary;
    }

    private Task<MessageBoxResult> ShowDirtyPromptAsync(string messageKey)
    {
        return MessageBoxHelper.ShowThreeOptionAsync(
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, messageKey),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "UnsavedChanges"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Save"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "DiscardChanges"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
            width: 600,
            minWidth: 560,
            primaryButtonIcon: SymbolRegular.Save24,
            secondaryButtonIcon: SymbolRegular.Delete24,
            closeButtonIcon: SymbolRegular.Dismiss24);
    }

    private async Task<bool> ConfirmResetToBuiltInAsync()
    {
        var messageBox = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "ResetToBuiltIn"),
            Content = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ResetLayoutConfirm"),
            PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
            PrimaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.ArrowClockwise24 },
            CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
            CloseButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 }
        };

        return await messageBox.ShowDialogAsync() == MessageBoxResult.Primary;
    }

    private void UpdateShiftSnapState()
    {
        _viewModel?.UpdateShiftSnapActive(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

}
