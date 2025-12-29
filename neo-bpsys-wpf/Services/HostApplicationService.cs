using System.IO;
using System.Reflection;
using System.Windows;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Plugins.Services;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using HostMessageBoxResult = neo_bpsys_wpf.Core.Plugins.Services.MessageBoxResult;
using HostMessageBoxButtons = neo_bpsys_wpf.Core.Plugins.Services.MessageBoxButtons;
using HostNotificationType = neo_bpsys_wpf.Core.Plugins.Services.NotificationType;
using CoreSnackbarService = neo_bpsys_wpf.Core.Abstractions.Services.ISnackbarService;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 宿主应用程序服务实现
/// </summary>
public class HostApplicationService : IHostApplicationService
{
    private readonly INavigationService _navigationService;
    private readonly IMessageBoxService _messageBoxService;
    private readonly CoreSnackbarService _snackbarService;

    public HostApplicationService(
        INavigationService navigationService,
        IMessageBoxService messageBoxService,
        CoreSnackbarService snackbarService)
    {
        _navigationService = navigationService;
        _messageBoxService = messageBoxService;
        _snackbarService = snackbarService;
    }

    /// <inheritdoc/>
    public Version HostVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    /// <inheritdoc/>
    public string HostName => "neo-bpsys-wpf";

    /// <inheritdoc/>
    public string AppDataDirectory => AppConstants.AppDataPath;

    /// <inheritdoc/>
    public string PluginsDirectory => Path.Combine(AppConstants.AppDataPath, "Plugins");

    /// <inheritdoc/>
    public string CurrentTheme => ApplicationThemeManager.GetAppTheme().ToString();

    /// <inheritdoc/>
    public string CurrentLanguage => WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture?.Name ?? "zh-CN";

    /// <inheritdoc/>
    public async Task<HostMessageBoxResult> ShowMessageBoxAsync(string message, string title, HostMessageBoxButtons buttons = HostMessageBoxButtons.OK)
    {
        var wpfButtons = buttons switch
        {
            HostMessageBoxButtons.OKCancel => MessageBoxButton.OKCancel,
            HostMessageBoxButtons.YesNo => MessageBoxButton.YesNo,
            HostMessageBoxButtons.YesNoCancel => MessageBoxButton.YesNoCancel,
            _ => MessageBoxButton.OK
        };

        System.Windows.MessageBoxResult result = System.Windows.MessageBoxResult.None;
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            result = MessageBox.Show(message, title, wpfButtons);
        });

        return result switch
        {
            System.Windows.MessageBoxResult.OK => HostMessageBoxResult.OK,
            System.Windows.MessageBoxResult.Cancel => HostMessageBoxResult.Cancel,
            System.Windows.MessageBoxResult.Yes => HostMessageBoxResult.Yes,
            System.Windows.MessageBoxResult.No => HostMessageBoxResult.No,
            _ => HostMessageBoxResult.None
        };
    }

    /// <inheritdoc/>
    public void ShowNotification(string title, string message, HostNotificationType type = HostNotificationType.Information)
    {
        var controlAppearance = type switch
        {
            HostNotificationType.Success => Wpf.Ui.Controls.ControlAppearance.Success,
            HostNotificationType.Warning => Wpf.Ui.Controls.ControlAppearance.Caution,
            HostNotificationType.Error => Wpf.Ui.Controls.ControlAppearance.Danger,
            _ => Wpf.Ui.Controls.ControlAppearance.Info
        };

        Application.Current.Dispatcher.Invoke(() =>
        {
            var textBlock = new System.Windows.Controls.TextBlock { Text = message };
            _snackbarService.Show(title, textBlock, controlAppearance, null, TimeSpan.FromSeconds(3), true);
        });
    }

    /// <inheritdoc/>
    public bool Navigate(Type pageType, object? parameter = null)
    {
        return Application.Current.Dispatcher.Invoke(() =>
        {
            return _navigationService.Navigate(pageType);
        });
    }

    /// <inheritdoc/>
    public void InvokeOnUIThread(Action action)
    {
        Application.Current.Dispatcher.Invoke(action);
    }

    /// <inheritdoc/>
    public async Task InvokeOnUIThreadAsync(Action action)
    {
        await Application.Current.Dispatcher.InvokeAsync(action);
    }
}
