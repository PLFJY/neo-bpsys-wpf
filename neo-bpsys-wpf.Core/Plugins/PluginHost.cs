using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Plugins.Commands;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Settings;
using neo_bpsys_wpf.Core.Plugins.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件宿主实现
/// </summary>
public sealed class PluginHost : IPluginHost
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Lazy<IPluginManager> _pluginManager;
    private readonly Lazy<IUIExtensionService> _uiExtensionService;
    private readonly Lazy<ICommandExtensionService> _commandExtensionService;
    private readonly Lazy<IPluginEventBus> _eventBus;
    private readonly Lazy<IPluginSettingsService> _settingsService;
    private readonly PluginSystemOptions _options;

    /// <inheritdoc/>
    public IPluginManager PluginManager => _pluginManager.Value;

    /// <inheritdoc/>
    public IUIExtensionService UIExtensionService => _uiExtensionService.Value;

    /// <inheritdoc/>
    public ICommandExtensionService CommandExtensionService => _commandExtensionService.Value;

    /// <inheritdoc/>
    public IPluginEventBus EventBus => _eventBus.Value;

    /// <inheritdoc/>
    public IPluginSettingsService SettingsService => _settingsService.Value;

    /// <inheritdoc/>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <inheritdoc/>
    public Version ApplicationVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);

    /// <inheritdoc/>
    public string ApplicationName => AppConstants.AppName;

    /// <inheritdoc/>
    public string UserDataDirectory => AppConstants.UserDataPath;

    /// <inheritdoc/>
    public string PluginDirectory => _options.PluginDirectory;

    /// <summary>
    /// 创建插件宿主
    /// </summary>
    public PluginHost(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _options = serviceProvider.GetService<PluginSystemOptions>() ?? new PluginSystemOptions();

        // 使用Lazy延迟初始化，避免循环依赖
        _pluginManager = new Lazy<IPluginManager>(() => serviceProvider.GetRequiredService<IPluginManager>());
        _uiExtensionService = new Lazy<IUIExtensionService>(() => serviceProvider.GetRequiredService<IUIExtensionService>());
        _commandExtensionService = new Lazy<ICommandExtensionService>(() => serviceProvider.GetRequiredService<ICommandExtensionService>());
        _eventBus = new Lazy<IPluginEventBus>(() => serviceProvider.GetRequiredService<IPluginEventBus>());
        _settingsService = new Lazy<IPluginSettingsService>(() => serviceProvider.GetRequiredService<IPluginSettingsService>());
    }

    /// <inheritdoc/>
    public T? GetService<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }

    /// <inheritdoc/>
    public T GetRequiredService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    /// <inheritdoc/>
    public void InvokeOnUIThread(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }

    /// <inheritdoc/>
    public async Task InvokeOnUIThreadAsync(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            await Application.Current.Dispatcher.InvokeAsync(action);
        }
    }

    /// <inheritdoc/>
    public void ShowNotification(string title, string message, NotificationSeverity severity = NotificationSeverity.Information)
    {
        // 这里可以集成应用程序的通知系统
        // 目前使用简单的消息框实现
        InvokeOnUIThread(() =>
        {
            var icon = severity switch
            {
                NotificationSeverity.Success => MessageBoxImage.Information,
                NotificationSeverity.Warning => MessageBoxImage.Warning,
                NotificationSeverity.Error => MessageBoxImage.Error,
                _ => MessageBoxImage.Information
            };

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        });
    }

    /// <inheritdoc/>
    public Task<DialogResult> ShowDialogAsync(string title, string message, DialogButtons buttons = DialogButtons.Ok)
    {
        return Task.Run(() =>
        {
            var result = DialogResult.None;

            InvokeOnUIThread(() =>
            {
                var msgBoxButtons = buttons switch
                {
                    DialogButtons.OkCancel => MessageBoxButton.OKCancel,
                    DialogButtons.YesNo => MessageBoxButton.YesNo,
                    DialogButtons.YesNoCancel => MessageBoxButton.YesNoCancel,
                    _ => MessageBoxButton.OK
                };

                var msgBoxResult = MessageBox.Show(message, title, msgBoxButtons);

                result = msgBoxResult switch
                {
                    MessageBoxResult.OK => DialogResult.Ok,
                    MessageBoxResult.Cancel => DialogResult.Cancel,
                    MessageBoxResult.Yes => DialogResult.Yes,
                    MessageBoxResult.No => DialogResult.No,
                    _ => DialogResult.None
                };
            });

            return result;
        });
    }
}
