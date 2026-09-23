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
/// FrontedDesignerWindow 的PropertyEditors业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private async void PropertyTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var committed = await ApplyPropertyEditorValueAsync(sender);
        if (committed)
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    private async void PropertyTextApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (await ApplyPropertyEditorValueAsync(sender))
        {
            FocusDesignSurface();
        }
    }

    private void PropertyTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not FrameworkElement editor || !ShouldAutoCommitPropertyEditor(editor))
        {
            return;
        }

        SchedulePropertyAutoCommit(editor);
    }

    private async void WindowBackgroundColorTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (_viewModel is not null
            && await _viewModel.ApplyWindowBackgroundColorEditAsync())
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    private async void WindowBackgroundColorApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null
            && await _viewModel.ApplyWindowBackgroundColorEditAsync())
        {
            FocusDesignSurface();
        }
    }

    private void BrowseBindingButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_bindingBrowserProvider is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var viewModel = new FrontedBindingBrowserWindowViewModel(
            _bindingBrowserProvider,
            new FrontedBindingTypeFilter(item.BindingTargetKind));
        var window = new FrontedBindingBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(item.EditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedBindingPath))
        {
            item.EditText = window.SelectedBindingPath;
            _viewModel?.ClearPropertyEditErrorForBufferUpdate(item.PropertyName);
        }
    }

    private async void BrowseResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(item.EditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            item.EditText = window.SelectedResourcePath;
            if (_viewModel is not null)
            {
                await _viewModel.ApplyPropertyResourceSelectionAsync(item, window.SelectedResourcePath);
            }
            FocusDesignSurface();
        }
    }

    private async void BrowseCanvasBackgroundResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null || _viewModel is null)
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(_viewModel.BackgroundImageEditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            await _viewModel.ApplyCanvasBackgroundResourceSelectionAsync(window.SelectedResourcePath);
            FocusDesignSurface();
        }
    }

    private async void BrowseAnimationPartImageResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null
            || _viewModel?.AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(editor.ImagePath);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            await _viewModel.ApplyAnimationPartImageResourceSelectionAsync(window.SelectedResourcePath);
        }
    }

    private async void ChooseLocalAnimationPartImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_filePickerService is null || _viewModel?.AnimationPartEditBuffer is not { IsImage: true })
        {
            return;
        }

        var file = _filePickerService.PickImage();
        if (!string.IsNullOrWhiteSpace(file))
        {
            await _viewModel.StoreLocalAnimationPartImageAsync(file);
        }
    }

    private async void ChooseLocalCanvasBackgroundButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_filePickerService is null || _viewModel is null)
        {
            return;
        }

        var file = _filePickerService.PickImage();
        if (!string.IsNullOrWhiteSpace(file))
        {
            await _viewModel.StoreLocalBackgroundImageAsync(file);
        }
    }

    private void PropertyCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPropertyEditorValue(sender);
    }

    private void PropertyComboBox_OnDropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyPropertyEditorValue(sender);
    }

    private void PropertyToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
    {
        ApplyPropertyEditorValue(sender);
    }

    private void PropertyFontComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox
            || e.AddedItems.Count == 0
            || !comboBox.IsDropDownOpen
            || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyFontComboBoxValue(comboBox, useSelectedOption: true);
    }

    private void PropertyFontComboBox_OnDropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyFontComboBoxValue(comboBox, useSelectedOption: true);
    }

    private void PropertyFontComboBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            ApplyFontComboBoxValue(comboBox, useSelectedOption: false);
        }
    }

    private void PropertyFontComboBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not ComboBox comboBox)
        {
            return;
        }

        var committed = ApplyFontComboBoxValue(comboBox, useSelectedOption: false);
        if (committed)
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    /// <summary>
    /// 处理 FontFamily 属性编辑器 Apply 按钮的点击事件。
    /// 复用 <see cref="ApplyFontComboBoxValue"/> 提交流程，与 Enter 键提交行为一致。
    /// </summary>
    /// <param name="sender">事件发送者（Apply 按钮）。</param>
    /// <param name="e">事件数据。</param>
    private void PropertyFontApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button
            || button.Parent is not Grid grid
            || grid.Children.OfType<ComboBox>().FirstOrDefault() is not { } comboBox)
        {
            return;
        }

        var committed = ApplyFontComboBoxValue(comboBox, useSelectedOption: false);
        if (committed)
        {
            FocusDesignSurface();
        }
    }

    private bool ApplyFontComboBoxValue(ComboBox comboBox, bool useSelectedOption)
    {
        if (IsPropertyEditorCommitSuppressed()
            || _viewModel is null
            || comboBox.DataContext is not FrontedPropertyEditorItem item)
        {
            return false;
        }

        var value = ResolveFontComboBoxValue(comboBox, useSelectedOption);
        item.Value = value;
        item.EditText = comboBox.Text;
        return _viewModel.ApplyPropertyEdit(item, value);
    }

    private static string ResolveFontComboBoxValue(ComboBox comboBox, bool useSelectedOption)
    {
        if (useSelectedOption && comboBox.SelectedItem is FrontedFontFamilyOption selectedOption)
        {
            return selectedOption.Value;
        }

        var text = comboBox.Text;
        var matchingOption = comboBox.Items
            .OfType<FrontedFontFamilyOption>()
            .FirstOrDefault(option => string.Equals(option.DisplayName, text, StringComparison.CurrentCultureIgnoreCase));
        return matchingOption?.Value ?? text;
    }

    private async void ImportFontButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_filePickerService is null
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var path = _filePickerService.PickFontFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _viewModel.ImportAndApplyPackageFontAsync(item, path);
    }

    private void ManagePackageFontsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_packageFontManagerViewModel is null || _viewModel is null)
        {
            return;
        }

        var window = new FrontedPackageFontManagerWindow(_packageFontManagerViewModel)
        {
            Owner = this
        };
        window.ShowDialog();
        _viewModel.RefreshFontFamilyEditorOptions();
    }

    private void PropertyColorPicker_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject picker)
        {
            return;
        }

        DependencyPropertyDescriptor
            .FromName("SelectedColor", picker.GetType(), picker.GetType())
            ?.AddValueChanged(picker, PropertyColorPicker_OnSelectedColorChanged);
    }

    private void PropertyColorPicker_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject picker)
        {
            return;
        }

        DependencyPropertyDescriptor
            .FromName("SelectedColor", picker.GetType(), picker.GetType())
            ?.RemoveValueChanged(picker, PropertyColorPicker_OnSelectedColorChanged);
    }

    private void PropertyColorPicker_OnSelectedColorChanged(object? sender, EventArgs e)
    {
        if (sender is not FrameworkElement picker || !picker.IsKeyboardFocusWithin)
        {
            return;
        }

        if (IsPropertyEditorCommitSuppressed())
        {
            return;
        }

        if (picker.DataContext is FrontedPropertyEditorItem item)
        {
            if (picker.GetType().GetProperty("SelectedColor")?.GetValue(picker) is Color selectedColor)
            {
                item.ColorValue = selectedColor;
            }

            item.EditText = FrontedPropertyColorHelper.ToArgbString(item.ColorValue);
            if (!item.RequiresExplicitCommit)
            {
                ApplyPropertyEditorValue(picker);
            }
        }
    }

    private bool ApplyPropertyEditorValue(object sender, bool refreshPropertyGrid = true)
    {
        if (IsPropertyEditorCommitSuppressed()
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return false;
        }

        var value = sender is System.Windows.Controls.TextBox textBox
            ? textBox.Text
            : item.EditorKind is FrontedPropertyEditorKind.Text
                or FrontedPropertyEditorKind.Number
                ? item.EditText
                : item.Value;

        return _viewModel.ApplyPropertyEdit(item, value, refreshPropertyGrid);
    }

    private async Task<bool> ApplyPropertyEditorValueAsync(object sender)
    {
        if (IsPropertyEditorCommitSuppressed()
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return false;
        }

        var value = sender is System.Windows.Controls.TextBox textBox
            ? textBox.Text
            : item.EditorKind is FrontedPropertyEditorKind.Text
                or FrontedPropertyEditorKind.Number
                ? item.EditText
                : item.Value;

        return await _viewModel.ApplyPropertyEditAsync(item, value);
    }

    private bool IsPropertyEditorCommitSuppressed()
    {
        return !_isLoaded || _suppressPropertyEditorCommit || _viewModel?.IsRebuildingPropertyGrid == true;
    }

    private void InitializePropertyAutoCommitTimer()
    {
        _propertyAutoCommitTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _propertyAutoCommitTimer.Tick += PropertyAutoCommitTimer_OnTick;
    }

    private void SchedulePropertyAutoCommit(FrameworkElement editor)
    {
        if (_propertyAutoCommitTimer is null)
        {
            return;
        }

        _pendingAutoCommitEditor = editor;
        _propertyAutoCommitTimer.Stop();
        _propertyAutoCommitTimer.Start();
    }

    private void PropertyAutoCommitTimer_OnTick(object? sender, EventArgs e)
    {
        _propertyAutoCommitTimer?.Stop();
        var editor = _pendingAutoCommitEditor;
        _pendingAutoCommitEditor = null;
        if (editor is not null && ShouldAutoCommitPropertyEditor(editor))
        {
            // 自动提交时不重建属性网格（refreshPropertyGrid: false），
            // 避免 PropertyEditorItems 集合重建导致 TextBox 容器被销毁、输入框失焦。
            // 验证消息的属性行更新延后到手动提交（Enter/Apply）或选择变化时。
            ApplyPropertyEditorValue(editor, refreshPropertyGrid: false);
        }
    }

    private bool ShouldAutoCommitPropertyEditor(FrameworkElement editor)
    {
        return !IsPropertyEditorCommitSuppressed()
               && editor.IsKeyboardFocusWithin
               && editor.DataContext is FrontedPropertyEditorItem
               {
                   IsReadOnly: false,
                   RequiresExplicitCommit: false,
                   EditorKind: FrontedPropertyEditorKind.Text
                       or FrontedPropertyEditorKind.Number
                       or FrontedPropertyEditorKind.Color
               };
    }

    private void SuppressPropertyEditorCommitForLayoutPass()
    {
        _suppressPropertyEditorCommit = true;
        Dispatcher.BeginInvoke(
            () => _suppressPropertyEditorCommit = false,
            DispatcherPriority.Loaded);
    }

    private void EditTextBindingButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_bindingBrowserProvider is null
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var window = new FrontedTextBindingEditorWindow(
            item.Value as Core.Models.FrontedLayout.Binding.FrontedTextBindingExpression,
            _bindingBrowserProvider)
        {
            Owner = this
        };

        if (window.ShowDialog() == true && window.Result is not null)
        {
            _viewModel.ApplyTextBindingEdit(item, window.Result);
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindVisibleDescendant<T>(DependencyObject parent)
        where T : FrameworkElement
    {
        if (parent is T { IsVisible: true } typed)
        {
            return typed;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var nested = FindVisibleDescendant<T>(VisualTreeHelper.GetChild(parent, i));
            if (nested != null)
            {
                return nested;
            }
        }

        if (parent is ContentControl { Content: DependencyObject content })
        {
            return FindVisibleDescendant<T>(content);
        }

        return null;
    }

}
