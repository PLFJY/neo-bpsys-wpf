using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Plugins.Abstractions;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Plugins.Loading;

namespace neo_bpsys_wpf.Plugins.Services;

/// <summary>
/// 插件管理器实现
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly PluginLoader _loader;
    private readonly IPluginEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;
    private readonly string _pluginsDirectory;
    private readonly string _disabledPluginsFile;
    private readonly HashSet<string> _disabledPlugins = [];

    public PluginManager(
        PluginLoader loader,
        IPluginEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<PluginManager> logger,
        string pluginsDirectory)
    {
        _loader = loader;
        _eventBus = eventBus;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pluginsDirectory = pluginsDirectory;
        _disabledPluginsFile = Path.Combine(pluginsDirectory, "disabled-plugins.json");

        LoadDisabledPluginsList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IPlugin> LoadedPlugins => _loader.GetLoadedPlugins();

    /// <inheritdoc/>
    public IPlugin? GetPlugin(string pluginId) => _loader.GetPlugin(pluginId);

    /// <inheritdoc/>
    public bool IsPluginLoaded(string pluginId) => _loader.IsPluginLoaded(pluginId);

    /// <inheritdoc/>
    public async Task<PluginLoadResult> LoadPluginAsync(string pluginPath, CancellationToken cancellationToken = default)
    {
        var result = _loader.LoadPlugin(pluginPath);

        if (result.Success && result.Plugin != null)
        {
            var plugin = result.Plugin;

            // 检查是否被禁用
            if (_disabledPlugins.Contains(plugin.Metadata.Id))
            {
                _logger.LogInformation("Plugin {PluginId} is disabled, skipping initialization", plugin.Metadata.Id);
                return result;
            }

            try
            {
                // 发布加载事件
                await _eventBus.PublishAsync(new PluginLoadedEvent
                {
                    PluginMetadata = plugin.Metadata,
                    SourcePluginId = plugin.Metadata.Id
                }, cancellationToken);

                // 初始化插件
                await plugin.InitializeAsync(_serviceProvider, cancellationToken);

                // 发布初始化事件
                await _eventBus.PublishAsync(new PluginInitializedEvent
                {
                    PluginMetadata = plugin.Metadata,
                    SourcePluginId = plugin.Metadata.Id
                }, cancellationToken);

                // 启动插件
                await plugin.StartAsync(cancellationToken);

                // 发布启动事件
                await _eventBus.PublishAsync(new PluginStartedEvent
                {
                    PluginMetadata = plugin.Metadata,
                    SourcePluginId = plugin.Metadata.Id
                }, cancellationToken);

                _logger.LogInformation("Plugin {PluginId} started successfully", plugin.Metadata.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize/start plugin {PluginId}", plugin.Metadata.Id);

                await _eventBus.PublishAsync(new PluginErrorEvent
                {
                    PluginMetadata = plugin.Metadata,
                    SourcePluginId = plugin.Metadata.Id,
                    ErrorMessage = ex.Message,
                    Exception = ex
                }, cancellationToken);

                return PluginLoadResult.Failed($"Plugin loaded but failed to initialize: {ex.Message}", ex);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var plugin = _loader.GetPlugin(pluginId);
        if (plugin == null)
        {
            _logger.LogWarning("Cannot unload plugin {PluginId}: not found", pluginId);
            return;
        }

        try
        {
            // 发布停止事件
            await _eventBus.PublishAsync(new PluginStoppedEvent
            {
                PluginMetadata = plugin.Metadata,
                SourcePluginId = plugin.Metadata.Id
            }, cancellationToken);

            await _loader.UnloadPluginAsync(pluginId);

            // 发布卸载事件
            await _eventBus.PublishAsync(new PluginUnloadedEvent
            {
                PluginMetadata = plugin.Metadata,
                SourcePluginId = plugin.Metadata.Id
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin {PluginId}", pluginId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        _disabledPlugins.Remove(pluginId);
        await SaveDisabledPluginsListAsync();

        var plugin = _loader.GetPlugin(pluginId);
        if (plugin != null && plugin.State != PluginState.Running)
        {
            await plugin.InitializeAsync(_serviceProvider, cancellationToken);
            await plugin.StartAsync(cancellationToken);

            await _eventBus.PublishAsync(new PluginStartedEvent
            {
                PluginMetadata = plugin.Metadata,
                SourcePluginId = plugin.Metadata.Id
            }, cancellationToken);
        }

        _logger.LogInformation("Plugin {PluginId} enabled", pluginId);
    }

    /// <inheritdoc/>
    public async Task DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        _disabledPlugins.Add(pluginId);
        await SaveDisabledPluginsListAsync();

        var plugin = _loader.GetPlugin(pluginId);
        if (plugin != null && plugin.State == PluginState.Running)
        {
            await plugin.StopAsync(cancellationToken);

            await _eventBus.PublishAsync(new PluginStoppedEvent
            {
                PluginMetadata = plugin.Metadata,
                SourcePluginId = plugin.Metadata.Id
            }, cancellationToken);
        }

        _logger.LogInformation("Plugin {PluginId} disabled", pluginId);
    }

    /// <inheritdoc/>
    public async Task ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var plugin = _loader.GetPlugin(pluginId);
        if (plugin == null)
        {
            _logger.LogWarning("Cannot reload plugin {PluginId}: not found", pluginId);
            return;
        }

        // 获取插件路径（需要在卸载前保存）
        var pluginDir = Path.Combine(_pluginsDirectory, pluginId);
        var pluginDll = Directory.GetFiles(pluginDir, "*.dll")
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(pluginId, StringComparison.OrdinalIgnoreCase))
            ?? Directory.GetFiles(pluginDir, "*.dll").FirstOrDefault();

        if (pluginDll == null)
        {
            _logger.LogError("Cannot find plugin DLL for reload: {PluginId}", pluginId);
            return;
        }

        await UnloadPluginAsync(pluginId, cancellationToken);
        await LoadPluginAsync(pluginDll, cancellationToken);

        _logger.LogInformation("Plugin {PluginId} reloaded", pluginId);
    }

    /// <inheritdoc/>
    public PluginState GetPluginState(string pluginId)
    {
        var plugin = _loader.GetPlugin(pluginId);
        if (plugin == null)
            return PluginState.NotLoaded;

        if (_disabledPlugins.Contains(pluginId))
            return PluginState.Disabled;

        return plugin.State;
    }

    /// <summary>
    /// 扫描并加载所有插件
    /// </summary>
    public async Task LoadAllPluginsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
            _logger.LogInformation("Created plugins directory: {Directory}", _pluginsDirectory);
            return;
        }

        var pluginDirectories = Directory.GetDirectories(_pluginsDirectory);

        foreach (var pluginDir in pluginDirectories)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var pluginName = Path.GetFileName(pluginDir);
            
            // 查找插件DLL（优先查找与目录同名的DLL）
            var pluginDll = Directory.GetFiles(pluginDir, "*.dll")
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                ?? Directory.GetFiles(pluginDir, "*.dll").FirstOrDefault();

            if (pluginDll == null)
            {
                _logger.LogWarning("No DLL found in plugin directory: {Directory}", pluginDir);
                continue;
            }

            try
            {
                await LoadPluginAsync(pluginDll, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Directory}", pluginDir);
            }
        }
    }

    private void LoadDisabledPluginsList()
    {
        try
        {
            if (File.Exists(_disabledPluginsFile))
            {
                var json = File.ReadAllText(_disabledPluginsFile);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    foreach (var id in list)
                    {
                        _disabledPlugins.Add(id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load disabled plugins list");
        }
    }

    private async Task SaveDisabledPluginsListAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_disabledPlugins.ToList());
            await File.WriteAllTextAsync(_disabledPluginsFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save disabled plugins list");
        }
    }
}
