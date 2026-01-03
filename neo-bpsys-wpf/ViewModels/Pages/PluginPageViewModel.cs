using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.Plugin;
using neo_bpsys_wpf.Locales;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class PluginPageViewModel : ViewModelBase
{
    private readonly IPluginService _pluginService;
    private readonly ILogger<PluginPageViewModel> _logger;

    public PluginPageViewModel(IPluginService pluginService, ILogger<PluginPageViewModel> logger)
    {
        _pluginService = pluginService;
        _logger = logger;
        PluginsCollection = new ObservableCollection<PluginInfo>(IPluginService.LoadedPlugins);
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling plugin uninstall state");
        }
    }

    private static bool CanUninstall(PluginInfo plugin) => !plugin.IsBuiltIn;

    [RelayCommand]
    private static async Task RestartApp()
    {
        if(await MessageBoxHelper.ShowConfirmAsync(Lang.RestartNeeded,
            Lang.SomeSettingsRequireRestartingTheApplication))
            App.Restart();
    }
}