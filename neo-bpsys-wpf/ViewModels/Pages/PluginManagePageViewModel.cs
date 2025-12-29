using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Plugins;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 插件管理页面视图模型
/// Plugin management page view model
/// </summary>
public partial class PluginManagePageViewModel : ViewModelBase
{
    private readonly IPluginService _pluginService;
    private readonly IMessageBoxService _messageBoxService;
    private readonly ILogger<PluginManagePageViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<PluginItemViewModel> _plugins = new();

    [ObservableProperty]
    private PluginItemViewModel? _selectedPlugin;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public PluginManagePageViewModel(
        IPluginService pluginService,
        IMessageBoxService messageBoxService,
        ILogger<PluginManagePageViewModel> logger)
    {
        _pluginService = pluginService ?? throw new ArgumentNullException(nameof(pluginService));
        _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _pluginService.PluginStateChanged += OnPluginStateChanged;

        // 初始化时加载插件
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await RefreshPluginsAsync();
    }

    [RelayCommand]
    private async Task RefreshPluginsAsync()
    {
        IsLoading = true;
        StatusMessage = "正在扫描插件...";

        try
        {
            var pluginDirectory = Path.Combine(AppConstants.AppDataPath, "Plugins");
            var count = await _pluginService.DiscoverPluginsAsync(pluginDirectory);

            Plugins.Clear();
            foreach (var metadata in _pluginService.LoadedPlugins)
            {
                Plugins.Add(new PluginItemViewModel(metadata, _pluginService, _messageBoxService));
            }

            StatusMessage = $"发现 {count} 个插件";
            _logger.LogInformation("Refreshed plugin list: {Count} plugins found", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing plugins");
            StatusMessage = $"刷新插件列表失败: {ex.Message}";
            await _messageBoxService.ShowErrorAsync("刷新插件列表失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadAllPluginsAsync()
    {
        IsLoading = true;
        StatusMessage = "正在加载所有插件...";

        try
        {
            var loadedCount = 0;
            var failedCount = 0;

            foreach (var plugin in Plugins.Where(p => p.State == PluginState.NotLoaded && p.IsEnabled))
            {
                var result = await _pluginService.LoadPluginAsync(plugin.Id);
                if (result.Success)
                {
                    loadedCount++;
                }
                else
                {
                    failedCount++;
                    _logger.LogWarning("Failed to load plugin {PluginId}: {Error}",
                        plugin.Id, result.ErrorMessage);
                }
            }

            StatusMessage = $"加载完成: 成功 {loadedCount} 个, 失败 {failedCount} 个";
            await _messageBoxService.ShowInfoAsync("插件加载完成",
                $"成功加载 {loadedCount} 个插件\n失败 {failedCount} 个插件");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugins");
            StatusMessage = $"加载插件失败: {ex.Message}";
            await _messageBoxService.ShowErrorAsync("加载插件失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StartAllPluginsAsync()
    {
        IsLoading = true;
        StatusMessage = "正在启动所有插件...";

        try
        {
            var startedCount = 0;
            var failedCount = 0;

            foreach (var plugin in Plugins.Where(p =>
                         (p.State == PluginState.Loaded || p.State == PluginState.Stopped) && p.IsEnabled))
            {
                if (await _pluginService.StartPluginAsync(plugin.Id))
                {
                    startedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            StatusMessage = $"启动完成: 成功 {startedCount} 个, 失败 {failedCount} 个";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting plugins");
            StatusMessage = $"启动插件失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenPluginFolderAsync()
    {
        try
        {
            var pluginDirectory = Path.Combine(AppConstants.AppDataPath, "Plugins");
            if (!Directory.Exists(pluginDirectory))
            {
                Directory.CreateDirectory(pluginDirectory);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = pluginDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening plugin folder");
            await _messageBoxService.ShowErrorAsync("打开插件文件夹失败", ex.Message);
        }
    }

    private void OnPluginStateChanged(object? sender, PluginStateChangedEventArgs e)
    {
        var plugin = Plugins.FirstOrDefault(p => p.Id == e.PluginId);
        if (plugin != null)
        {
            plugin.State = e.NewState;
            plugin.ErrorMessage = e.ErrorMessage;
        }
    }
}

/// <summary>
/// 插件项视图模型
/// Plugin item view model
/// </summary>
public partial class PluginItemViewModel : ObservableObject
{
    private readonly IPluginService _pluginService;
    private readonly IMessageBoxService _messageBoxService;

    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string _version;

    [ObservableProperty]
    private string _author;

    [ObservableProperty]
    private PluginState _state;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string? _errorMessage;

    public string StateDisplayName => State switch
    {
        PluginState.NotLoaded => "未加载",
        PluginState.Loaded => "已加载",
        PluginState.Initializing => "初始化中",
        PluginState.Running => "运行中",
        PluginState.Stopped => "已停止",
        PluginState.Error => "错误",
        PluginState.Unloaded => "已卸载",
        _ => "未知"
    };

    public bool CanLoad => State == PluginState.NotLoaded && IsEnabled;
    public bool CanStart => (State == PluginState.Loaded || State == PluginState.Stopped) && IsEnabled;
    public bool CanStop => State == PluginState.Running;
    public bool CanUnload => State is PluginState.Loaded or PluginState.Stopped or PluginState.Error;

    public PluginItemViewModel(
        PluginMetadata metadata,
        IPluginService pluginService,
        IMessageBoxService messageBoxService)
    {
        _pluginService = pluginService;
        _messageBoxService = messageBoxService;
        _id = metadata.Id;
        _name = metadata.Name;
        _description = metadata.Description;
        _version = metadata.Version.ToString();
        _author = metadata.Author;
        _state = metadata.State;
        _isEnabled = metadata.IsEnabled;
    }

    partial void OnStateChanged(PluginState value)
    {
        OnPropertyChanged(nameof(StateDisplayName));
        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanUnload));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanStart));
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync()
    {
        var result = await _pluginService.LoadPluginAsync(Id);
        if (!result.Success)
        {
            await _messageBoxService.ShowErrorAsync("加载插件失败", result.ErrorMessage ?? "未知错误");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!await _pluginService.StartPluginAsync(Id))
        {
            await _messageBoxService.ShowErrorAsync("启动插件失败", $"无法启动插件: {Name}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        if (!await _pluginService.StopPluginAsync(Id))
        {
            await _messageBoxService.ShowErrorAsync("停止插件失败", $"无法停止插件: {Name}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanUnload))]
    private async Task UnloadAsync()
    {
        if (!await _pluginService.UnloadPluginAsync(Id))
        {
            await _messageBoxService.ShowErrorAsync("卸载插件失败", $"无法卸载插件: {Name}");
        }
    }

    [RelayCommand]
    private async Task ToggleEnableAsync()
    {
        if (IsEnabled)
        {
            await _pluginService.DisablePluginAsync(Id);
            IsEnabled = false;
        }
        else
        {
            await _pluginService.EnablePluginAsync(Id);
            IsEnabled = true;
        }
    }
}
