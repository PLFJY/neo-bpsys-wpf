using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using System.Collections.ObjectModel;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class PluginPageViewModel
{
    private bool _isPluginMarketInitialized;

    [ObservableProperty]
    private ObservableCollection<PluginMarketItem> _marketPluginsCollection;

    [ObservableProperty]
    private PluginMarketItem? _selectedMarketPlugin;

    [ObservableProperty]
    private bool _isMarketLoading;

    [ObservableProperty]
    private string _marketErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _isPluginMarketSettingsOpen;

    [ObservableProperty]
    private string _selectedPluginMarketMirror = string.Empty;

    [ObservableProperty]
    private double _pluginDownloadProgress;

    [ObservableProperty]
    private string _pluginDownloadProgressText = string.Empty;

    [ObservableProperty]
    private string _pluginDownloadSpeedText = string.Empty;

    public ObservableCollection<PluginMarketMirrorOption> PluginMarketMirrorOptions { get; } = [];

    public bool HasMarketError => !string.IsNullOrWhiteSpace(MarketErrorMessage);

    public bool IsMarketPluginSelected => SelectedMarketPlugin != null;

    public bool IsPluginDownloadVisible =>
        SelectedMarketPlugin != null
        && SelectedMarketPlugin.Id == _pluginMarketService.CurrentDownloadPluginId
        && _pluginMarketService.IsDownloading;

    partial void OnMarketErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasMarketError));
    }

    partial void OnSelectedMarketPluginChanged(PluginMarketItem? value)
    {
        OnPropertyChanged(nameof(IsMarketPluginSelected));
        RefreshDownloadState();
        _ = LoadSelectedPluginReadmeAsync(value);
    }

    partial void OnSelectedPluginMarketMirrorChanged(string value)
    {
        if (!_isPluginMarketInitialized || _settingsHostService == null)
        {
            return;
        }

        _ = PersistPluginMarketMirrorAsync(value);
    }

    private void InitializePluginMarket()
    {
        PluginMarketMirrorOptions.Clear();
        foreach (var mirror in DownloadMirrorPresets.GhProxyMirrorList)
        {
            PluginMarketMirrorOptions.Add(new PluginMarketMirrorOption
            {
                DisplayName = string.IsNullOrWhiteSpace(mirror) ? "直接连接（不使用代理）" : mirror,
                Value = mirror
            });
        }

        if (!PluginMarketMirrorOptions.Any(x => x.Value == _settingsHostService.Settings.PluginMarketMirror))
        {
            PluginMarketMirrorOptions.Insert(0, new PluginMarketMirrorOption
            {
                DisplayName = _settingsHostService.Settings.PluginMarketMirror,
                Value = _settingsHostService.Settings.PluginMarketMirror
            });
        }

        SelectedPluginMarketMirror = _settingsHostService.Settings.PluginMarketMirror;
        _pluginMarketService.DownloadStateChanged += PluginMarketService_DownloadStateChanged;
        _isPluginMarketInitialized = true;
        _ = RefreshMarketAsync();
    }

    private void PluginMarketService_DownloadStateChanged(object? sender, EventArgs e)
    {
        if (App.Current.Dispatcher.CheckAccess())
        {
            RefreshDownloadState();
        }
        else
        {
            App.Current.Dispatcher.Invoke(RefreshDownloadState);
        }
    }

    private void RefreshDownloadState()
    {
        PluginDownloadProgress = _pluginMarketService.DownloadProgress;
        PluginDownloadProgressText = _pluginMarketService.IsDownloading
            ? $"{_pluginMarketService.DownloadProgress:0.00}%"
            : string.Empty;
        PluginDownloadSpeedText = _pluginMarketService.IsDownloading
            ? $"{(_pluginMarketService.DownloadBytesPerSecond / 1024 / 1024):0.00} MB/s"
            : string.Empty;
        OnPropertyChanged(nameof(IsPluginDownloadVisible));
    }

    [RelayCommand]
    private async Task RefreshMarketAsync()
    {
        var selectedId = SelectedMarketPlugin?.Id;
        try
        {
            IsMarketLoading = true;
            MarketErrorMessage = string.Empty;
            var items = await _pluginMarketService.GetMarketPluginsAsync();
            MarketPluginsCollection = new ObservableCollection<PluginMarketItem>(items);
            RefreshMarketPluginStates();
            SelectedMarketPlugin = string.IsNullOrWhiteSpace(selectedId)
                ? null
                : MarketPluginsCollection.FirstOrDefault(x => x.Id == selectedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when loading plugin market");
            MarketErrorMessage = ex.Message;
        }
        finally
        {
            IsMarketLoading = false;
        }
    }

    [RelayCommand]
    private void TogglePluginMarketSettings()
    {
        IsPluginMarketSettingsOpen = !IsPluginMarketSettingsOpen;
    }

    [RelayCommand]
    private async Task ExecutePrimaryMarketActionAsync()
    {
        if (SelectedMarketPlugin == null
            || !SelectedMarketPlugin.CanExecutePrimaryAction
            || _pluginMarketService.IsDownloading)
        {
            return;
        }

        try
        {
            var result = await _pluginMarketService.DownloadPluginPackageAsync(SelectedMarketPlugin);
            InstallPluginFromExtractedDirectory(result.ExtractedDirectoryPath);
            RefreshMarketPluginStates();
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消下载时静默结束。
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when downloading plugin from market");
            await MessageBoxHelper.ShowErrorAsync(ex.Message);
        }
    }

    [RelayCommand]
    private void ToggleSelectedMarketPluginUninstall()
    {
        if (SelectedMarketPlugin?.LocalPlugin == null)
        {
            return;
        }

        ToggleUninstall(SelectedMarketPlugin.LocalPlugin);
        RefreshMarketPluginStates();
    }

    [RelayCommand]
    private void CloseMarketPluginDetails()
    {
        SelectedMarketPlugin = null;
    }

    [RelayCommand]
    private void CancelPluginMarketDownload()
    {
        _pluginMarketService.CancelDownload();
    }

    private async Task PersistPluginMarketMirrorAsync(string value)
    {
        _settingsHostService.Settings.PluginMarketMirror = value;
        _pluginMarketService.ResetMirrorCache();
        await _settingsHostService.SaveConfigAsync();
        await RefreshMarketAsync();
    }

    private async Task LoadSelectedPluginReadmeAsync(PluginMarketItem? item)
    {
        if (item == null || item.IsReadmeLoading || !string.IsNullOrWhiteSpace(item.ReadmeMarkdown))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.ResolvedReadmeUrl))
        {
            item.ReadmeMarkdown = "暂无 README";
            return;
        }

        item.IsReadmeLoading = true;
        item.ReadmeMarkdown = "README 加载中...";
        try
        {
            item.ReadmeMarkdown = await _pluginMarketService.GetReadmeMarkdownAsync(item);
        }
        catch (Exception ex)
        {
            item.ReadmeMarkdown = $"# README 加载失败\n\n{ex.Message}";
        }
        finally
        {
            item.IsReadmeLoading = false;
        }
    }

    private void RefreshMarketPluginStates()
    {
        foreach (var item in MarketPluginsCollection)
        {
            ApplyMarketPluginState(item);
        }
    }

    private void ApplyMarketPluginState(PluginMarketItem item)
    {
        var localPlugin = PluginsCollection.FirstOrDefault(x => x.Manifest.Id == item.Id);
        var compatibility = PluginApiVersionHelper.Evaluate(item.ApiVersion);
        item.LocalPlugin = localPlugin;
        item.IsInstalled = localPlugin != null;
        item.CanUninstall = localPlugin != null;
        item.UninstallActionText = localPlugin?.IsUninstalling == true ? "取消卸载" : "卸载";
        item.IsApiCompatible = compatibility.IsCompatible;
        item.IsHostVersionTooLow = compatibility.IsTooHigh;
        item.IsApiTooLow = compatibility.IsTooLow || !compatibility.IsFormatValid;
        item.CompatibilityMessage = compatibility.Message;

        if (localPlugin == null)
        {
            item.HasUpdateAvailable = false;
        }
        else
        {
            item.HasUpdateAvailable = CompareVersion(item.Version, GetDisplayedLocalPluginVersion(localPlugin)) > 0;
        }

        if (!compatibility.IsCompatible)
        {
            item.PrimaryActionText = compatibility.IsTooHigh ? "版本过低" : "版本不兼容";
            item.CanExecutePrimaryAction = false;
            item.MarketStatusText = compatibility.IsTooHigh ? "当前软件版本过低" : "API 不兼容";
            item.IsStatusVisible = true;
            return;
        }

        if (item.HasUpdateAvailable)
        {
            item.PrimaryActionText = "更新";
            item.CanExecutePrimaryAction = true;
            item.MarketStatusText = "有更新";
            item.IsStatusVisible = true;
            return;
        }

        if (item.IsInstalled)
        {
            item.PrimaryActionText = "已安装";
            item.CanExecutePrimaryAction = false;
            item.MarketStatusText = localPlugin?.IsUninstalling == true ? "待卸载" : "已安装";
            item.IsStatusVisible = true;
            return;
        }

        item.PrimaryActionText = "安装";
        item.CanExecutePrimaryAction = true;
        item.MarketStatusText = string.Empty;
        item.IsStatusVisible = false;
    }

    private void InstallPluginFromExtractedDirectory(string tempFolderPath)
    {
        var manifestPath = Path.Combine(tempFolderPath, "manifest.yml");

        if (!File.Exists(manifestPath))
        {
            throw new Exception(I18nHelper.GetLocalizedString("CannotFindManifest"));
        }

        var manifestYml = File.ReadAllText(manifestPath);
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var manifest = deserializer.Deserialize<PluginManifest?>(manifestYml);
        if (manifest == null)
        {
            throw new Exception(I18nHelper.GetLocalizedString("ManifestNotValid"));
        }

        var compatibility = PluginApiVersionHelper.Evaluate(manifest.ApiVersion);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(compatibility.IsTooHigh ? "当前软件版本过低，无法安装此插件。" : compatibility.Message);
        }

        var pluginFolderPath = Path.Combine(AppConstants.PluginPath, manifest.Id);
        if (Directory.Exists(pluginFolderPath))
        {
            pluginFolderPath = Path.Combine(AppConstants.PluginPath, ".new", manifest.Id);
            if (!Directory.Exists(Path.Combine(AppConstants.PluginPath, ".new")))
            {
                Directory.CreateDirectory(Path.Combine(AppConstants.PluginPath, ".new"));
            }

            if (Directory.Exists(pluginFolderPath))
            {
                Directory.Delete(pluginFolderPath, true);
            }

            Directory.Move(tempFolderPath, pluginFolderPath);

            var local = PluginsCollection.First(x => x.Manifest.Id == manifest.Id);
            local.IsRestartRequired = true;
            local.NewVersion = manifest.Version;
            local.IsNewVersionInstalled = true;
            IsRestartNeeded = true;
            return;
        }

        var info = new PluginInfo
        {
            Manifest = manifest,
            IsLocal = true,
            PluginFolderPath = pluginFolderPath,
            RealIconPath = Path.Combine(Path.GetFullPath(pluginFolderPath), manifest.Icon),
            IsRestartRequired = true
        };

        Directory.Move(tempFolderPath, pluginFolderPath);
        PluginsCollection.Add(info);
        IsRestartNeeded = true;
    }

    private static string GetDisplayedLocalPluginVersion(PluginInfo plugin)
    {
        if (plugin.IsNewVersionInstalled && !string.IsNullOrWhiteSpace(plugin.NewVersion))
        {
            return plugin.NewVersion;
        }

        return plugin.Manifest.Version;
    }

    private static int CompareVersion(string remoteVersion, string localVersion)
    {
        if (Version.TryParse(remoteVersion, out var remote) && Version.TryParse(localVersion, out var local))
        {
            return remote.CompareTo(local);
        }

        return string.Compare(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase);
    }
}
