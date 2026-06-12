using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Views.Windows;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SettingPageViewModel> _logger;
    private FrontedBehaviorEventDebuggerWindow? _behaviorEventDebuggerWindow;
    public IUpdaterService UpdaterService { get; }

    public List<OpenSourceRepo> OpenSourceRepoColumn1 { get; }
    public List<OpenSourceRepo> OpenSourceRepoColumn2 { get; }
    public List<OpenSourceRepo> OpenSourceRepoColumn3 { get; }

    public SettingPageViewModel(
        IUpdaterService updaterService,
        ISettingsHostService settingsHostService,
        IPluginMarketService pluginMarketService,
        IServiceProvider serviceProvider,
        ILogger<SettingPageViewModel> logger)
    {
        AppVersion = AppConstants.AppVersion;
        UpdaterService = updaterService;
        _settingsHostService = settingsHostService;
        _pluginMarketService = pluginMarketService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        UpdaterService.DownloadStateChanged += UpdaterService_DownloadStateChanged;
        RefreshUpdateDownloadState();
        _settingsHostService.Settings.PropertyChanged += Settings_PropertyChanged;
        SyncMirrorFromSettings();

        SelectedLanguage = _settingsHostService.Settings.Language;
        _isSyncingLogLevel = true;
        SelectedLogLevel = _settingsHostService.Settings.LogLevel;
        _isSyncingLogLevel = false;

        var columns = SplitIntoColumns(CreateOpenSourceRepos(), 3);
        OpenSourceRepoColumn1 = columns[0];
        OpenSourceRepoColumn2 = columns[1];
        OpenSourceRepoColumn3 = columns[2];
    }

    public bool IsClassicMode
    {
        get => _settingsHostService.Settings.IsClassicMode;
        set
        {
            if (_settingsHostService.Settings.IsClassicMode == value)
            {
                return;
            }

            _settingsHostService.Settings.IsClassicMode = value;
            OnPropertyChanged();
            _ = SaveClassicModeAndOfferRestartAsync();
        }
    }

    private async Task SaveClassicModeAndOfferRestartAsync()
    {
        await _settingsHostService.SaveConfigAsync();

        var shouldRestart = await MessageBoxHelper.ShowConfirmAsync(
            I18nHelper.GetLocalizedString("ClassicModeRestartRequired"),
            I18nHelper.GetLocalizedString("Warning"),
            I18nHelper.GetLocalizedString("RestartNow"),
            I18nHelper.GetLocalizedString("Cancel"));

        if (!shouldRestart)
        {
            return;
        }

        AppBase.Current.Restart();
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

    /// <summary>
    /// Opens the global behavior event debugger window.
    /// </summary>
    [RelayCommand]
    private void OpenBehaviorEventDebugger()
    {
        if (_behaviorEventDebuggerWindow is { IsLoaded: true })
        {
            _behaviorEventDebuggerWindow.Activate();
            return;
        }

        try
        {
            var window = ActivatorUtilities.CreateInstance<FrontedBehaviorEventDebuggerWindow>(_serviceProvider);
            window.Closed += (_, _) => _behaviorEventDebuggerWindow = null;
            _behaviorEventDebuggerWindow = window;
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open behavior event debugger window.");
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("WindowLaunchError")}\n{ex.Message}");
        }
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

    private static List<List<OpenSourceRepo>> SplitIntoColumns(IReadOnlyList<OpenSourceRepo> sorted, int columnCount)
    {
        var columns = new List<List<OpenSourceRepo>>(columnCount);
        int totalItems = sorted.Count;
        int rows = (int)Math.Ceiling((double)totalItems / columnCount);

        for (int col = 0; col < columnCount; col++)
        {
            var columnItems = new List<OpenSourceRepo>(rows);
            for (int row = 0; row < rows; row++)
            {
                int index = row * columnCount + col;
                if (index < totalItems)
                {
                    columnItems.Add(sorted[index]);
                }
            }
            columns.Add(columnItems);
        }
        return columns;
    }

    private static List<OpenSourceRepo> CreateOpenSourceRepos()
    {
        var repos = new List<OpenSourceRepo>
        {
            new() { Name = ".Net Runtime", Url = "https://github.com/dotnet/runtime/" },
            new() { Name = "CommunityToolkit.Mvvm", Url = "https://github.com/CommunityToolkit/dotnet/" },
            new() { Name = "Downloader", Url = "https://github.com/bezzad/Downloader/" },
            new() { Name = "hyjiacan.pinyin4net", Url = "https://gitee.com/hyjiacan/Pinyin4Net/" },
            new() { Name = "OpenCvSharp", Url = "https://github.com/shimat/opencvsharp/" },
            new() { Name = "PixiEditor.ColorPicker", Url = "https://github.com/PixiEditor/ColorPicker/" },
            new() { Name = "Sdcb.PaddleOCR", Url = "https://github.com/sdcb/PaddleSharp/" },
            new() { Name = "UI.WPF.Modern", Url = "https://github.com/iNKORE-NET/UI.WPF.Modern" },
            new() { Name = "Windows Presentation Foundation (WPF)", Url = "https://github.com/dotnet/wpf/" },
            new() { Name = "WPF UI", Url = "https://github.com/lepoco/wpfui/" },
            new() { Name = "WpfGorgeousThemeSwitch", Url = "https://github.com/SunnyDesignor/WpfGorgeousThemeSwitch/" },
            new() { Name = "WPFLocalizeExtension", Url = "https://github.com/XAMLMarkupExtensions/WPFLocalizeExtension" },
            new() { Name = "XamlBehaviors for WPF", Url = "https://github.com/microsoft/XamlBehaviorsWpf/" },
        };
        repos.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        return repos;
    }
}
