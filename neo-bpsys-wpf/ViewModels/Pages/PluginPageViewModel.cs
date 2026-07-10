using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 插件页面视图模型，管理插件列表的显示、启用/禁用、卸载以及从文件安装插件。
/// </summary>
public partial class PluginPageViewModel : ViewModelBase
{
    private readonly IPluginService _pluginService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogger<PluginPageViewModel> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly IPluginMarketService _pluginMarketService;
    private readonly IInfoBarService _infoBarService;
    private readonly IPluginInstallService _pluginInstallService;

#pragma warning disable CS8618 
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
    public PluginPageViewModel()
#pragma warning restore CS8618 
    {
        PluginsCollection = [];
        MarketPluginsCollection = [];
    }

    /// <summary>
    /// 初始化插件页面视图模型。
    /// </summary>
    /// <param name="pluginService">插件服务</param>
    /// <param name="filePickerService">文件选择服务</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="settingsHostService">设置宿主服务</param>
    /// <param name="pluginMarketService">插件市场服务</param>
    /// <param name="infoBarService">信息栏服务</param>
    /// <param name="pluginInstallService">插件安装服务</param>
    public PluginPageViewModel(IPluginService pluginService, IFilePickerService filePickerService,
        ILogger<PluginPageViewModel> logger, ISettingsHostService settingsHostService, IPluginMarketService pluginMarketService,
        IInfoBarService infoBarService,
        IPluginInstallService pluginInstallService)
    {
        _pluginService = pluginService;
        _filePickerService = filePickerService;
        _logger = logger;
        _settingsHostService = settingsHostService;
        _pluginMarketService = pluginMarketService;
        _infoBarService = infoBarService;
        _pluginInstallService = pluginInstallService;
        PluginsCollection = new ObservableCollection<PluginInfo>(IPluginService.LoadedPlugins);
        MarketPluginsCollection = [];
        InitializePluginMarket();
    }

    /// <summary>
    /// 获取或设置是否需要重启以生效插件更改。
    /// </summary>
    [ObservableProperty]
    public partial bool IsRestartNeeded { get; set; }

    /// <summary>
    /// 获取或设置已加载插件列表。
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<PluginInfo> PluginsCollection { get; set; }

    [RelayCommand]
    private void ToggleEnable(PluginInfo plugin)
    {
        try
        {
            plugin.IsEnabled = !plugin.IsEnabled;
            IsRestartNeeded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling plugin enabled state");
        }
    }

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private void ToggleUninstall(PluginInfo plugin)
    {
        try
        {
            plugin.IsUninstalling = !plugin.IsUninstalling;
            IsRestartNeeded = plugin.IsRestartRequired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling plugin uninstall state");
        }
    }

    private static bool CanUninstall(PluginInfo plugin) => !plugin.IsBuiltIn;

    [RelayCommand]
    private static async Task RestartAppAsync()
    {
        if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "SomeSettingsRequireRestartingTheApplication"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "RestartNeeded"), I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"), I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
            AppBase.Current.Restart();
    }

    /// <summary>
    /// 临时文件路径
    /// </summary>
    private static readonly string TempPath = Path.Combine(AppConstants.AppTempPath, "PluginPackage");


    [RelayCommand]
    private void InstallPluginFromFile()
    {
        //准备插件压缩包路径
        var pluginFile = _filePickerService.PickPluginPackageFile();
        if (pluginFile == null) return;

        var tempFolderPath = Path.Combine(TempPath, Path.GetFileNameWithoutExtension(pluginFile));

        //如果存在先删除
        if (Directory.Exists(tempFolderPath))
            Directory.Delete(tempFolderPath, true);

        try
        {
            var result = _pluginInstallService.InstallFromArchive(pluginFile, tempFolderPath);
            UpdateLocalPluginState(result);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error when installing plugin from file");
            _ = MessageBoxHelper.ShowErrorAsync(e.Message);
            if (Directory.Exists(tempFolderPath))
            {
                Directory.Delete(tempFolderPath, true);
            }
        }
    }
}
