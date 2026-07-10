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
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Services.SmartBpModule;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 设置页面视图模型，管理应用版本、自动更新、语言、调试选项、开源依赖和经典模式等设置。
/// </summary>
public partial class SettingPageViewModel : ViewModelBase
{
    private bool _isSyncingLogLevel;

    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
#pragma warning disable CS8618
    public SettingPageViewModel()
#pragma warning restore CS8618
    {
    }

    private readonly ISettingsHostService _settingsHostService;
    private readonly IPluginMarketService _pluginMarketService;
    private readonly IBpuiFileAssociationService _bpuiFileAssociationService;
    private readonly IFilePickerService _filePickerService;
    private readonly SmartBpModuleManager _smartBpModuleManager;
    private readonly ITutorialStateManager _tutorialStateManager;
    private readonly ITutorialRunner _tutorialRunner;
    private readonly IOnboardingCoordinator _onboardingCoordinator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SettingPageViewModel> _logger;
    private FrontedBehaviorEventDebuggerWindow? _behaviorEventDebuggerWindow;

    /// <summary>
    /// 获取更新服务。
    /// </summary>
    public IUpdaterService UpdaterService { get; }

    /// <summary>
    /// 开源依赖列表第一列。
    /// </summary>
    public List<OpenSourceRepo> OpenSourceRepoColumn1 { get; }
    /// <summary>
    /// 开源依赖列表第二列。
    /// </summary>
    public List<OpenSourceRepo> OpenSourceRepoColumn2 { get; }
    /// <summary>
    /// 开源依赖列表第三列。
    /// </summary>
    public List<OpenSourceRepo> OpenSourceRepoColumn3 { get; }

    /// <summary>
    /// 初始化设置页面视图模型。
    /// </summary>
    /// <param name="updaterService">更新服务</param>
    /// <param name="settingsHostService">设置宿主服务</param>
    /// <param name="pluginMarketService">插件市场服务</param>
    /// <param name="bpuiFileAssociationService">bpui 文件关联服务</param>
    /// <param name="filePickerService">文件选择服务</param>
    /// <param name="smartBpModuleManager">SmartBP 模块管理器</param>
    /// <param name="tutorialStateManager">教程状态管理器</param>
    /// <param name="tutorialRunner">教程运行器</param>
    /// <param name="onboardingCoordinator">首次导览协调器</param>
    /// <param name="serviceProvider">服务Provider</param>
    /// <param name="logger">日志记录器</param>
    public SettingPageViewModel(
        IUpdaterService updaterService,
        ISettingsHostService settingsHostService,
        IPluginMarketService pluginMarketService,
        IBpuiFileAssociationService bpuiFileAssociationService,
        IFilePickerService filePickerService,
        SmartBpModuleManager smartBpModuleManager,
        ITutorialStateManager tutorialStateManager,
        ITutorialRunner tutorialRunner,
        IOnboardingCoordinator onboardingCoordinator,
        IServiceProvider serviceProvider,
        ILogger<SettingPageViewModel> logger)
    {
        AppVersion = AppConstants.AppVersion;
        UpdaterService = updaterService;
        _settingsHostService = settingsHostService;
        _pluginMarketService = pluginMarketService;
        _bpuiFileAssociationService = bpuiFileAssociationService;
        _filePickerService = filePickerService;
        _smartBpModuleManager = smartBpModuleManager;
        _tutorialStateManager = tutorialStateManager;
        _tutorialRunner = tutorialRunner;
        _onboardingCoordinator = onboardingCoordinator;
        _serviceProvider = serviceProvider;
        _logger = logger;

        UpdaterService.DownloadStateChanged += UpdaterService_DownloadStateChanged;
        RefreshUpdateDownloadState();
        _settingsHostService.Settings.PropertyChanged += Settings_PropertyChanged;
        SyncMirrorFromSettings();

        SelectedLanguage = _settingsHostService.Settings.Language;
        SmartBpModuleRoot = _smartBpModuleManager.GetPreferredModuleRoot();
        _isSyncingLogLevel = true;
        SelectedLogLevel = _settingsHostService.Settings.LogLevel;
        _isSyncingLogLevel = false;

        var columns = SplitIntoColumns(CreateOpenSourceRepos(), 3);
        OpenSourceRepoColumn1 = columns[0];
        OpenSourceRepoColumn2 = columns[1];
        OpenSourceRepoColumn3 = columns[2];
    }

    /// <summary>
    /// 获取或设置是否将 .bpui 布局包文件关联到本应用。
    /// </summary>
    public bool AssociateBpuiFiles
    {
        get => _settingsHostService.Settings.AssociateBpuiFiles;
        set
        {
            if (_settingsHostService.Settings.AssociateBpuiFiles == value)
            {
                return;
            }

            _settingsHostService.Settings.AssociateBpuiFiles = value;
            OnPropertyChanged();
            _bpuiFileAssociationService.EnsureAssociationState(value);
            _ = _settingsHostService.SaveConfigAsync();
        }
    }

    /// <summary>
    /// 获取或设置是否使用经典模式。切换后需要重启生效。
    /// </summary>
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
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "ClassicModeRestartRequired"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Warning"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "RestartNow"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));

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

    #region 教程与导览

    /// <summary>
    /// 重新启动首次导览。
    /// </summary>
    [RelayCommand]
    private async Task RestartFirstRunTutorialAsync()
    {
        var confirmed = await MessageBoxHelper.ShowConfirmAsync(
            "确定要重新启动首次导览吗？当前操作会清除首次导览完成状态。",
            "教程与导览",
            "重新启动",
            "取消");
        if (!confirmed || Application.Current.MainWindow is not Window owner)
        {
            return;
        }

        await _onboardingCoordinator.RestartFirstRunFlowAsync(owner);
    }

    /// <summary>
    /// 重置全部教程状态。
    /// </summary>
    [RelayCommand]
    private async Task ResetAllTutorialStateAsync()
    {
        var confirmed = await MessageBoxHelper.ShowConfirmAsync(
            "确定要重置全部教程状态吗？所有已完成、已跳过和被总导览覆盖的记录都会被清空。",
            "教程与导览",
            "重置",
            "取消");
        if (!confirmed)
        {
            return;
        }

        await _tutorialStateManager.ResetStateAsync();
    }

    /// <summary>
    /// 运行真实页面目标与操作信号验证导览。
    /// </summary>
    [RelayCommand]
    private async Task RunRealTargetProbeTutorialAsync()
    {
        if (Application.Current.MainWindow is not Window owner)
        {
            return;
        }

        owner.Activate();
        await _tutorialRunner.RunFlowAsync(owner, TutorialFlowIds.Phase4RealTargetProbe, force: true);
    }

    #endregion

    #region 调试选项

    /// <summary>
    /// 当前选择的日志级别。
    /// </summary>
    [ObservableProperty]
    public partial AppLogLevel SelectedLogLevel { get; set; }

    /// <summary>
    /// 可选日志级别字典，键为本地化 Key，值为对应级别。
    /// </summary>
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
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowLaunchError")}\n{ex.Message}");
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
            new() { Name = ".Net Runtime", Url = "https://github.com/dotnet/runtime" },
            new() { Name = "CommunityToolkit.Mvvm", Url = "https://github.com/CommunityToolkit/dotnet" },
            new() { Name = "Downloader", Url = "https://github.com/bezzad/Downloader" },
            new() { Name = "hyjiacan.pinyin4net", Url = "https://gitee.com/hyjiacan/Pinyin4Net" },
            new() { Name = "OpenCvSharp", Url = "https://github.com/shimat/opencvsharp" },
            new() { Name = "PixiEditor.ColorPicker", Url = "https://github.com/PixiEditor/ColorPicker" },
            new() { Name = "Sdcb.PaddleOCR", Url = "https://github.com/sdcb/PaddleSharp" },
            new() { Name = "UI.WPF.Modern", Url = "https://github.com/iNKORE-NET/UI.WPF.Modern" },
            new() { Name = "Windows Presentation Foundation (WPF)", Url = "https://github.com/dotnet/wpf" },
            new() { Name = "WPF UI", Url = "https://github.com/lepoco/wpfui" },
            new() { Name = "WpfGorgeousThemeSwitch", Url = "https://github.com/SunnyDesignor/WpfGorgeousThemeSwitch" },
            new() { Name = "WPFLocalizeExtension", Url = "https://github.com/XAMLMarkupExtensions/WPFLocalizeExtension" },
            new() { Name = "XamlBehaviors for WPF", Url = "https://github.com/microsoft/XamlBehaviorsWpf" },
            new() { Name = "SharpCompress", Url = "https://github.com/adamhathcock/sharpcompress" },
        };
        repos.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        return repos;
    }
}
