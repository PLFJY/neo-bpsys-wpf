using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Services.Abstractions;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel : ViewModelBase
{
    private bool _isSyncingLogLevel;

#pragma warning disable CS8618
    public SettingPageViewModel()
#pragma warning restore CS8618
    {
    }

    private readonly ISettingsHostService _settingsHostService;
    private readonly IPluginMarketService _pluginMarketService;
    public IUpdaterService UpdaterService { get; }

    public SettingPageViewModel(IUpdaterService updaterService, ISettingsHostService settingsHostService,
        IPluginMarketService pluginMarketService)
    {
        AppVersion = AppConstants.AppVersion;
        UpdaterService = updaterService;
        _settingsHostService = settingsHostService;
        _pluginMarketService = pluginMarketService;

        UpdaterService.DownloadStateChanged += UpdaterService_DownloadStateChanged;
        RefreshUpdateDownloadState();
        _settingsHostService.Settings.PropertyChanged += Settings_PropertyChanged;
        SyncMirrorFromSettings();

        SelectedLanguage = _settingsHostService.Settings.Language;
        _isSyncingLogLevel = true;
        SelectedLogLevel = _settingsHostService.Settings.LogLevel;
        _isSyncingLogLevel = false;
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(_settingsHostService.Settings.GhProxyMirror))
        {
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            SyncMirrorFromSettings();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(SyncMirrorFromSettings);
        }
    }

    #region 调试选项

    [ObservableProperty]
    private AppLogLevel _selectedLogLevel;

    public Dictionary<string, AppLogLevel> LogLevelOptions { get; } = new()
    {
        { "LogLevelVerbose", AppLogLevel.Verbose },
        { "LogLevelDebug", AppLogLevel.Debug },
        { "LogLevelInformation", AppLogLevel.Information },
        { "LogLevelWarning", AppLogLevel.Warning },
        { "LogLevelError", AppLogLevel.Error },
        { "LogLevelFatal", AppLogLevel.Fatal }
    };

    partial void OnSelectedLogLevelChanged(AppLogLevel value)
    {
        if (_isSyncingLogLevel || _settingsHostService == null)
        {
            return;
        }

        _settingsHostService.Settings.LogLevel = value;
        App.ApplyLogLevel(value);
        _ = _settingsHostService.SaveConfigAsync();
    }

    /// <summary>
    /// 手动触发GC (调试选项)
    /// </summary>
    [RelayCommand]
    private static void ManualGc()
    {
        GC.Collect();
    }

    /// <summary>
    /// 跳转到日志目录
    /// </summary>
    [RelayCommand]
    private static void HopToLogDir()
    {
        Process.Start("explorer.exe", AppConstants.LogPath);
    }

    /// <summary>
    /// 打开启动提示
    /// </summary>
    [RelayCommand]
    private void OpenTip()
    {
        _settingsHostService.Settings.ShowAfterUpdateTip = true;
        _ = _settingsHostService.SaveConfigAsync();
        _ = MessageBoxHelper.ShowInfoAsync("Settings.ShowTip has been set to true");
    }

    #endregion

    #region 快捷入口

    /// <summary>
    /// 跳转到配置目录
    /// </summary>
    [RelayCommand]
    private static void HopToConfigDir()
    {
        Process.Start("explorer.exe", AppConstants.AppDataPath);
    }

    /// <summary>
    /// 跳转到游戏输出目录
    /// </summary>
    [RelayCommand]
    private static void HopToGameOutputDir()
    {
        var path = Path.Combine(AppConstants.AppOutputPath, "GameInfoOutput");
        Process.Start("explorer.exe", path);
    }

    #endregion
}
