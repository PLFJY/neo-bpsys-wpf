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
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class PluginPageViewModel : ViewModelBase
{
    private readonly IPluginService _pluginService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogger<PluginPageViewModel> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly IPluginMarketService _pluginMarketService;

#pragma warning disable CS8618 
    public PluginPageViewModel()
#pragma warning restore CS8618 
    {
        PluginsCollection = [];
        MarketPluginsCollection = [];
    }

    public PluginPageViewModel(IPluginService pluginService, IFilePickerService filePickerService,
        ILogger<PluginPageViewModel> logger, ISettingsHostService settingsHostService, IPluginMarketService pluginMarketService)
    {
        _pluginService = pluginService;
        _filePickerService = filePickerService;
        _logger = logger;
        _settingsHostService = settingsHostService;
        _pluginMarketService = pluginMarketService;
        PluginsCollection = new ObservableCollection<PluginInfo>(IPluginService.LoadedPlugins);
        MarketPluginsCollection = [];
        InitializePluginMarket();
    }

    [ObservableProperty] private bool _isRestartNeeded;

    [ObservableProperty] private ObservableCollection<PluginInfo> _pluginsCollection;

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
        if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("SomeSettingsRequireRestartingTheApplication"),
            I18nHelper.GetLocalizedString("RestartNeeded"), I18nHelper.GetLocalizedString("Confirm"), I18nHelper.GetLocalizedString("Cancel")))
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
        var pluginFile = _filePickerService.PickZipFile();
        if (pluginFile == null) return;

        var tempFolderPath = Path.Combine(TempPath, Path.GetFileNameWithoutExtension(pluginFile));

        //如果存在先删除
        if (Directory.Exists(tempFolderPath))
            Directory.Delete(tempFolderPath, true);

        try
        {
            //解压压缩包
            ZipFile.ExtractToDirectory(pluginFile, tempFolderPath);
            InstallPluginFromExtractedDirectory(tempFolderPath);
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
