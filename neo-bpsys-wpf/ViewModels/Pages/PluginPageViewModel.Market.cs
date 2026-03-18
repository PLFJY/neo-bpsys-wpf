using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Security.Cryptography;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class PluginPageViewModel
{
    /// <summary>
    /// 标记插件市场功能是否已经初始化完成。
    /// </summary>
    private bool _isPluginMarketInitialized;

    /// <summary>
    /// 防止同步镜像设置时重复保存。
    /// </summary>
    private bool _isSyncingGlobalMirror;

    /// <summary>
    /// 当前插件市场列表。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PluginMarketItem> _marketPluginsCollection;

    /// <summary>
    /// 当前选中的插件市场条目。
    /// </summary>
    [ObservableProperty]
    private PluginMarketItem? _selectedMarketPlugin;

    /// <summary>
    /// 插件市场列表是否正在加载。
    /// </summary>
    [ObservableProperty]
    private bool _isMarketLoading;

    /// <summary>
    /// 插件市场加载失败时显示的错误消息。
    /// </summary>
    [ObservableProperty]
    private string _marketErrorMessage = string.Empty;

    /// <summary>
    /// 插件市场设置面板是否打开。
    /// </summary>
    [ObservableProperty]
    private bool _isPluginMarketSettingsOpen;

    /// <summary>
    /// 下载队列面板是否打开。
    /// </summary>
    [ObservableProperty]
    private bool _isDownloadQueueOpen;

    /// <summary>
    /// 当前选中的下载镜像地址。
    /// </summary>
    [ObservableProperty]
    private string _selectedPluginMarketMirror = string.Empty;

    /// <summary>
    /// 当前正在下载的插件进度值。
    /// </summary>
    [ObservableProperty]
    private double _pluginDownloadProgress;

    /// <summary>
    /// 当前正在下载的插件进度文本。
    /// </summary>
    [ObservableProperty]
    private string _pluginDownloadProgressText = string.Empty;

    /// <summary>
    /// 当前正在下载的插件速度文本。
    /// </summary>
    [ObservableProperty]
    private string _pluginDownloadSpeedText = string.Empty;

    /// <summary>
    /// 插件市场镜像选项列表。
    /// </summary>
    public ObservableCollection<PluginMarketMirrorOption> PluginMarketMirrorOptions { get; } =
        new(DownloadMirrorPresets.GhProxyMirrorList.Select(
            mirror => new PluginMarketMirrorOption
            {
                DisplayNameKey = string.IsNullOrWhiteSpace(mirror)
                    ? "PluginMarketDirectConnectionNoProxy"
                    : mirror,
                Value = mirror
            }));

    /// <summary>
    /// 插件下载队列。
    /// </summary>
    public ReadOnlyObservableCollection<PluginDownloadQueueItem> PluginDownloadQueue => _pluginMarketService.DownloadQueue;

    /// <summary>
    /// 当前是否存在插件市场加载错误。
    /// </summary>
    public bool HasMarketError => !string.IsNullOrWhiteSpace(MarketErrorMessage);

    /// <summary>
    /// 当前是否选中了插件详情。
    /// </summary>
    public bool IsMarketPluginSelected => SelectedMarketPlugin != null;

    /// <summary>
    /// 插件市场中的任一浮层是否处于打开状态。
    /// </summary>
    public bool HasPluginMarketOverlay => IsPluginMarketSettingsOpen || IsDownloadQueueOpen || IsMarketPluginSelected;

    /// <summary>
    /// 下载队列中是否存在任何任务。
    /// </summary>
    public bool HasPluginDownloadQueueItems => PluginDownloadQueue.Count > 0;

    /// <summary>
    /// 下载队列中是否存在仍在进行中的任务。
    /// </summary>
    public bool HasActivePluginDownloadQueueItems => PluginDownloadQueueCount > 0;

    /// <summary>
    /// 下载队列角标数量。
    /// </summary>
    public int PluginDownloadQueueCount => PluginDownloadQueue.Count(x => x.IsInProgress);

    /// <summary>
    /// 当前选中插件的下载进度条是否显示。
    /// </summary>
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
        OnPropertyChanged(nameof(HasPluginMarketOverlay));
        RefreshDownloadState();
        _ = LoadSelectedPluginReadmeAsync(value);
    }

    partial void OnIsPluginMarketSettingsOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPluginMarketOverlay));
    }

    partial void OnIsDownloadQueueOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPluginMarketOverlay));
    }

    /// <summary>
    /// 保存插件市场镜像设置。
    /// </summary>
    partial void OnSelectedPluginMarketMirrorChanged(string value)
    {
        if (!_isPluginMarketInitialized || _settingsHostService == null || _isSyncingGlobalMirror)
        {
            return;
        }

        _ = PersistGhProxyMirrorAsync(value);
    }

    /// <summary>
    /// 初始化插件市场相关功能。
    /// </summary>
    private void InitializePluginMarket()
    {
        _settingsHostService.Settings.PropertyChanged += Settings_PropertyChanged;
        if (PluginDownloadQueue is INotifyCollectionChanged notifyCollectionChanged)
        {
            notifyCollectionChanged.CollectionChanged += PluginDownloadQueue_CollectionChanged;
        }
        SelectedPluginMarketMirror = _settingsHostService.Settings.GhProxyMirror;
        _pluginMarketService.DownloadStateChanged += PluginMarketService_DownloadStateChanged;
        _isPluginMarketInitialized = true;
        _ = RefreshMarketAsync();
    }

    /// <summary>
    /// 刷新下载队列相关的显示状态。
    /// </summary>
    private void PluginDownloadQueue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasPluginDownloadQueueItems));
        OnPropertyChanged(nameof(HasActivePluginDownloadQueueItems));
        OnPropertyChanged(nameof(PluginDownloadQueueCount));
    }

    /// <summary>
    /// 同步插件市场镜像设置。
    /// </summary>
    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Core.Models.Settings.GhProxyMirror))
        {
            return;
        }

        if (App.Current.Dispatcher.CheckAccess())
        {
            SyncPluginMarketMirrorFromSettings();
        }
        else
        {
            App.Current.Dispatcher.Invoke(SyncPluginMarketMirrorFromSettings);
        }
    }

    /// <summary>
    /// 处理插件下载状态变化。
    /// </summary>
    private void PluginMarketService_DownloadStateChanged(object? sender, EventArgs e)
    {
        if (App.Current.Dispatcher.CheckAccess())
        {
            RefreshDownloadState();
            TryInstallCompletedPluginDownload();
        }
        else
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                RefreshDownloadState();
                TryInstallCompletedPluginDownload();
            });
        }
    }

    /// <summary>
    /// 刷新当前下载进度显示。
    /// </summary>
    private void RefreshDownloadState()
    {
        PluginDownloadProgress = _pluginMarketService.DownloadProgress;
        PluginDownloadProgressText = _pluginMarketService.IsDownloading
            ? $"{_pluginMarketService.DownloadProgress:0.00}%"
            : string.Empty;
        PluginDownloadSpeedText = _pluginMarketService.IsDownloading
            ? $"{(_pluginMarketService.DownloadBytesPerSecond / 1024 / 1024):0.00} MB/s"
            : string.Empty;
        OnPropertyChanged(nameof(HasActivePluginDownloadQueueItems));
        OnPropertyChanged(nameof(PluginDownloadQueueCount));
        OnPropertyChanged(nameof(IsPluginDownloadVisible));
    }

    /// <summary>
    /// 重新加载插件市场列表。
    /// </summary>
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

    /// <summary>
    /// 打开或关闭插件市场设置面板。
    /// </summary>
    [RelayCommand]
    private void TogglePluginMarketSettings()
    {
        if (!IsPluginMarketSettingsOpen)
        {
            IsDownloadQueueOpen = false;
            SelectedMarketPlugin = null;
        }

        IsPluginMarketSettingsOpen = !IsPluginMarketSettingsOpen;
    }

    /// <summary>
    /// 打开或关闭下载队列面板。
    /// </summary>
    [RelayCommand]
    private void ToggleDownloadQueue()
    {
        if (!IsDownloadQueueOpen)
        {
            IsPluginMarketSettingsOpen = false;
            SelectedMarketPlugin = null;
        }

        IsDownloadQueueOpen = !IsDownloadQueueOpen;
    }

    /// <summary>
    /// 关闭插件市场中的所有浮层面板。
    /// </summary>
    [RelayCommand]
    private void ClosePluginMarketOverlays()
    {
        IsDownloadQueueOpen = false;
        IsPluginMarketSettingsOpen = false;
        SelectedMarketPlugin = null;
    }

    /// <summary>
    /// 将当前选中的插件加入下载队列。
    /// </summary>
    [RelayCommand]
    private async Task ExecutePrimaryMarketActionAsync()
    {
        if (SelectedMarketPlugin == null
            || !SelectedMarketPlugin.CanExecutePrimaryAction)
        {
            return;
        }

        try
        {
            _ = await _pluginMarketService.QueuePluginDownloadAsync(SelectedMarketPlugin);
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

    /// <summary>
    /// 取消指定下载任务。
    /// </summary>
    [RelayCommand]
    private void CancelPluginMarketQueueItem(PluginDownloadQueueItem? item)
    {
        if (item == null)
        {
            return;
        }

        _pluginMarketService.CancelDownload(item.QueueId);
    }

    /// <summary>
    /// 保存插件市场镜像设置。
    /// </summary>
    private async Task PersistGhProxyMirrorAsync(string value)
    {
        _settingsHostService.Settings.GhProxyMirror = value;
        _pluginMarketService.ResetMirrorCache();
        await _settingsHostService.SaveConfigAsync();
    }

    /// <summary>
    /// 加载选中插件的 README。
    /// </summary>
    private async Task LoadSelectedPluginReadmeAsync(PluginMarketItem? item)
    {
        if (item == null || item.IsReadmeLoading || !string.IsNullOrWhiteSpace(item.ReadmeMarkdown))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.ResolvedReadmeUrl))
        {
            item.ReadmeMarkdown = string.Empty;
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

    /// <summary>
    /// 刷新插件市场中每个插件的显示状态。
    /// </summary>
    private void RefreshMarketPluginStates()
    {
        foreach (var item in MarketPluginsCollection)
        {
            ApplyMarketPluginState(item);
        }
    }

    /// <summary>
    /// 更新单个插件在市场中的显示状态。
    /// </summary>
    private void ApplyMarketPluginState(PluginMarketItem item)
    {
        var localPlugin = PluginsCollection.FirstOrDefault(x => x.Manifest.Id == item.Id);
        var compatibility = PluginApiVersionHelper.Evaluate(item.ApiVersion);
        item.LocalPlugin = localPlugin;
        item.IsInstalled = localPlugin != null;
        item.CanUninstall = localPlugin != null;
        item.UninstallActionKey = localPlugin?.IsUninstalling == true
            ? "CancelUninstall"
            : "Uninstall";
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
            item.PrimaryActionKey = compatibility.IsTooHigh
                ? "PluginMarketVersionTooLow"
                : "PluginMarketVersionIncompatible";
            item.CanExecutePrimaryAction = false;
            item.MarketStatusKey = compatibility.IsTooHigh
                ? "PluginMarketHostVersionTooLow"
                : "PluginMarketApiIncompatible";
            item.IsStatusVisible = true;
            return;
        }

        if (item.HasUpdateAvailable)
        {
            item.PrimaryActionKey = "Update";
            item.CanExecutePrimaryAction = true;
            item.MarketStatusKey = "PluginMarketUpdateAvailable";
            item.IsStatusVisible = true;
            return;
        }

        if (item.IsInstalled)
        {
            item.PrimaryActionKey = "Installed";
            item.CanExecutePrimaryAction = false;
            item.MarketStatusKey = localPlugin?.IsUninstalling == true
                ? "PluginMarketPendingUninstall"
                : "Installed";
            item.IsStatusVisible = true;
            return;
        }

        item.PrimaryActionKey = "Install";
        item.CanExecutePrimaryAction = true;
        item.MarketStatusKey = string.Empty;
        item.IsStatusVisible = false;
    }

    /// <summary>
    /// 安装一个已经解压好的插件包。
    /// </summary>
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

        ValidatePluginIntegrity(manifest, tempFolderPath);

        var compatibility = PluginApiVersionHelper.Evaluate(manifest.ApiVersion);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(compatibility.IsTooHigh
                ? I18nHelper.GetLocalizedString("PluginMarketInstallBlockedHostVersionTooLow")
                : compatibility.Message);
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

            var local = PluginsCollection.FirstOrDefault(x => x.Manifest.Id == manifest.Id);
            if (local != null)
            {
                local.IsRestartRequired = true;
                local.NewVersion = manifest.Version;
                local.IsNewVersionInstalled = true;
            }
            else
            {
                _logger.LogWarning(
                    "Plugin directory already exists for {PluginId}, but no matching plugin info was found in the current collection. Update was staged and will apply after restart.",
                    manifest.Id);
            }

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

    /// <summary>
    /// 校验插件入口程序集的 SHA-256。
    /// 如果清单中提供了 <c>sha256</c>，则必须与入口程序集文件的实际哈希一致；
    /// 否则会阻止安装，避免篡改后的插件进入插件目录。
    /// </summary>
    /// <param name="manifest">插件清单。</param>
    /// <param name="extractedDirectoryPath">插件解压目录。</param>
    /// <exception cref="InvalidOperationException">
    /// 当入口程序集缺失或哈希值与清单不一致时抛出。
    /// </exception>
    private static void ValidatePluginIntegrity(PluginManifest manifest, string extractedDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            return;
        }

        var entranceAssemblyPath = Path.Combine(extractedDirectoryPath, manifest.EntranceAssembly);
        if (!File.Exists(entranceAssemblyPath))
        {
            throw new InvalidOperationException(
                FormatLocalized("PluginMarketHashTargetMissing", manifest.EntranceAssembly));
        }

        var expectedHash = NormalizeSha256(manifest.Sha256);
        var actualHash = ComputeFileSha256(entranceAssemblyPath);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                FormatLocalized("PluginMarketSha256Mismatch", manifest.EntranceAssembly, expectedHash, actualHash));
        }
    }

    /// <summary>
    /// 计算指定文件的 SHA-256，并以小写十六进制字符串返回。
    /// </summary>
    /// <param name="filePath">待计算哈希的文件路径。</param>
    /// <returns>文件的 SHA-256 字符串。</returns>
    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// 规范化清单中的 SHA-256 文本。
    /// 允许在清单中使用大小写混合或带连字符的写法，比较前统一转成连续小写十六进制。
    /// </summary>
    /// <param name="value">原始哈希文本。</param>
    /// <returns>规范化后的 SHA-256 字符串。</returns>
    private static string NormalizeSha256(string value)
    {
        return value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    /// <summary>
    /// 清理插件包解压阶段留下的临时目录。
    /// 成功安装时会删除空的下载会话目录；
    /// 安装失败时会直接删除解压目录及其父级临时目录，避免残留继续堆积在临时目录下。
    /// </summary>
    /// <param name="extractedDirectoryPath">下载完成后用于安装的解压目录。</param>
    private static void CleanupDownloadedPluginPackageResidue(string extractedDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(extractedDirectoryPath))
        {
            return;
        }

        try
        {
            if (Directory.Exists(extractedDirectoryPath))
            {
                Directory.Delete(extractedDirectoryPath, true);
            }

            var sessionDirectory = Directory.GetParent(extractedDirectoryPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(sessionDirectory)
                && Directory.Exists(sessionDirectory)
                && !Directory.EnumerateFileSystemEntries(sessionDirectory).Any())
            {
                Directory.Delete(sessionDirectory, true);
            }
        }
        catch
        {
            // 临时目录清理失败不影响主流程，不在这里打断安装结果提示。
        }
    }

    /// <summary>
    /// 获取插件当前应显示的版本号。
    /// </summary>
    private static string GetDisplayedLocalPluginVersion(PluginInfo plugin)
    {
        if (plugin.IsNewVersionInstalled && !string.IsNullOrWhiteSpace(plugin.NewVersion))
        {
            return plugin.NewVersion;
        }

        return plugin.Manifest.Version;
    }

    /// <summary>
    /// 比较插件市场版本号和本地版本号。
    /// </summary>
    private static int CompareVersion(string remoteVersion, string localVersion)
    {
        if (Version.TryParse(remoteVersion, out var remote) && Version.TryParse(localVersion, out var local))
        {
            return remote.CompareTo(local);
        }

        return string.Compare(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 同步镜像设置到插件市场页面。
    /// </summary>
    private void SyncPluginMarketMirrorFromSettings()
    {
        _isSyncingGlobalMirror = true;
        try
        {
            SelectedPluginMarketMirror = _settingsHostService.Settings.GhProxyMirror;
        }
        finally
        {
            _isSyncingGlobalMirror = false;
        }
    }

    private static string FormatLocalized(string key, params object[] args)
    {
        return string.Format(I18nHelper.GetLocalizedString(key), args);
    }

    /// <summary>
    /// 安装所有已经下载完成的插件包。
    /// </summary>
    private void TryInstallCompletedPluginDownload()
    {
        while (true)
        {
            var result = _pluginMarketService.ConsumeCompletedDownload();
            if (result == null)
            {
                return;
            }

            try
            {
                InstallPluginFromExtractedDirectory(result.ExtractedDirectoryPath);
                RefreshMarketPluginStates();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when installing downloaded plugin package");
                _ = MessageBoxHelper.ShowErrorAsync(ex.Message);
            }
            finally
            {
                CleanupDownloadedPluginPackageResidue(result.ExtractedDirectoryPath);
            }
        }
    }
}
