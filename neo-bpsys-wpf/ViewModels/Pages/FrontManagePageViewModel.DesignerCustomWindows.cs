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
/// 前台管理页的DesignerCustomWindows逻辑。
/// </summary>
public sealed partial class FrontManagePageViewModel
{
    [RelayCommand]
    private async Task OpenFrontedDesignerAsync(string? selectedWindowId = null)
    {
        if (_serviceProvider is null)
        {
            return;
        }

        if (_frontedDesignerWindow is { IsLoaded: true })
        {
            _frontedDesignerWindow.Activate();
            await _frontedDesignerWindow.RefreshWindowCatalogAsync();
            if (!string.IsNullOrWhiteSpace(selectedWindowId)
                && _frontedDesignerWindow.DataContext is FrontedDesignerWindowViewModel existingDesignerViewModel)
            {
                existingDesignerViewModel.SelectWindow(selectedWindowId);
            }
            return;
        }

        try
        {
            var window = ActivatorUtilities.CreateInstance<FrontedDesignerWindow>(_serviceProvider);
            window.Owner = Application.Current?.MainWindow;
            EventHandler? closedHandler = null;
            closedHandler = (_, _) =>
            {
                window.Closed -= closedHandler;
                _frontedDesignerWindow = null;
            };
            window.Closed += closedHandler;
            _frontedDesignerWindow = window;
            try
            {
                window.Show();
                window.Activate();
                if (!string.IsNullOrWhiteSpace(selectedWindowId)
                    && window.DataContext is FrontedDesignerWindowViewModel designerViewModel)
                {
                    designerViewModel.SelectWindow(selectedWindowId);
                }
                TutorialSignalPublisher.Publish(TutorialSignalIds.FrontManageOpenDesignerClicked);
            }
            catch
            {
                window.Closed -= closedHandler;
                _frontedDesignerWindow = null;
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open fronted designer window.");
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowLaunchError")}\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreateCustomWindowAsync()
    {
        if (_packageManager is null || _customWindowSynchronizer is null || _serviceProvider is null)
        {
            return;
        }

        var generatedWindowId = $"window-{Guid.NewGuid():N}";
        var idBox = new System.Windows.Controls.TextBox
        {
            MinWidth = 280,
            Text = generatedWindowId,
            IsEnabled = false
        };
        var zhBox = new System.Windows.Controls.TextBox { MinWidth = 280 };
        var enBox = new System.Windows.Controls.TextBox { MinWidth = 280, IsEnabled = false };
        var jaBox = new System.Windows.Controls.TextBox { MinWidth = 280, IsEnabled = false };
        var advanced = new System.Windows.Controls.CheckBox
        {
            Content = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowAdvancedMode")
        };
        var settings = _settingsHostService?.Settings;
        var language = FrontedWindowDisplayNameResolver.ResolveConcreteLanguage(
            settings?.Language ?? LanguageKey.System,
            settings?.CultureInfo) ?? LanguageKey.zh_Hans;
        var content = new System.Windows.Controls.StackPanel { MinWidth = 360 };
        AddField(content, "CustomWindowId", idBox, out _);
        var zhField = AddField(content, "CustomWindowName", zhBox, out var zhLabel);
        var enField = AddField(content, "CustomWindowName", enBox, out var enLabel);
        var jaField = AddField(content, "CustomWindowName", jaBox, out var jaLabel);
        content.Children.Add(advanced);
        ApplyLanguageFields(showAllLanguages: false);
        advanced.Checked += (_, _) =>
        {
            idBox.IsEnabled = true;
            ApplyLanguageFields(showAllLanguages: true);
        };
        advanced.Unchecked += (_, _) =>
        {
            idBox.Text = generatedWindowId;
            idBox.IsEnabled = false;
            ApplyLanguageFields(showAllLanguages: false);
        };

        var dialog = new ContentDialog
        {
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CreateCustomWindow"),
            Content = content,
            PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
            CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")
        };
        var contentDialogService = _serviceProvider.GetService<IContentDialogService>();
        if (contentDialogService is null || await contentDialogService.ShowAsync(dialog) is not ContentDialogResult.Primary)
        {
            return;
        }

        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["zh_Hans"] = language == LanguageKey.zh_Hans ? zhBox.Text : string.Empty,
            ["en_US"] = language == LanguageKey.en_US ? enBox.Text : string.Empty,
            ["ja_JP"] = language == LanguageKey.ja_JP ? jaBox.Text : string.Empty
        };
        if (advanced.IsChecked == true)
        {
            displayNames["zh_Hans"] = zhBox.Text;
            displayNames["en_US"] = enBox.Text;
            displayNames["ja_JP"] = jaBox.Text;
        }

        try
        {
            var registration = await _packageManager.CreateCustomWindowAsync(new FrontedCustomWindowCreateRequest
            {
                WindowId = idBox.Text,
                DisplayNames = displayNames
            });
            await _customWindowSynchronizer.RefreshAsync();
            await RefreshCustomWindowViewsAsync();
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            await RefreshPackagesCoreAsync(registration.PackageScopeId);
            await OpenFrontedDesignerAsync(registration.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create custom fronted window.");
            PackageManagerStatus = ex.Message;
        }

        static System.Windows.Controls.StackPanel AddField(
            System.Windows.Controls.Panel panel,
            string labelKey,
            System.Windows.Controls.Control control,
            out System.Windows.Controls.TextBlock label)
        {
            var field = new System.Windows.Controls.StackPanel();
            label = new System.Windows.Controls.TextBlock
            {
                Margin = new Thickness(0, 0, 0, 4),
                Text = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, labelKey)
            };
            field.Children.Add(label);
            field.Children.Add(control);
            field.Children.Add(new System.Windows.Controls.Border { Height = 8, Background = null });
            panel.Children.Add(field);
            return field;
        }

        void ApplyLanguageFields(bool showAllLanguages)
        {
            var showChinese = showAllLanguages || language == LanguageKey.zh_Hans;
            var showEnglish = showAllLanguages || language == LanguageKey.en_US;
            var showJapanese = showAllLanguages || language == LanguageKey.ja_JP;

            zhField.Visibility = showChinese
                ? Visibility.Visible
                : Visibility.Collapsed;
            enField.Visibility = showEnglish
                ? Visibility.Visible
                : Visibility.Collapsed;
            jaField.Visibility = showJapanese
                ? Visibility.Visible
                : Visibility.Collapsed;

            zhBox.IsEnabled = showChinese;
            enBox.IsEnabled = showEnglish;
            jaBox.IsEnabled = showJapanese;

            zhLabel.Text = showAllLanguages
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowNameChinese")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowName");
            enLabel.Text = showAllLanguages
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowNameEnglish")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowName");
            jaLabel.Text = showAllLanguages
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowNameJapanese")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowName");
        }
    }

    [RelayCommand]
    private async Task ConfirmDeleteCustomWindowAsync(FrontedWindowManageItem? item)
    {
        if (item is not { IsCustom: true, IsCreatePlaceholder: false })
        {
            return;
        }

        await DeleteCustomWindowAsync(item.WindowId);
    }

    private async Task DeleteCustomWindowAsync(string windowId)
    {
        if (_packageManager is null || _customWindowSynchronizer is null || string.IsNullOrWhiteSpace(windowId))
        {
            return;
        }

        try
        {
            if (_frontedDesignerWindow is { IsLoaded: true }
                && !await _frontedDesignerWindow.PrepareForWindowRemovalAsync(windowId))
            {
                return;
            }

            await _packageManager.DeleteCustomWindowAsync(windowId);
            await _customWindowSynchronizer.RefreshAsync();
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            await RefreshCustomWindowViewsAsync(reloadDesignerLayout: true);
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CustomWindowDeleted");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete custom fronted window {WindowId}.", windowId);
            PackageManagerStatus = ex.Message;
        }
    }

}
