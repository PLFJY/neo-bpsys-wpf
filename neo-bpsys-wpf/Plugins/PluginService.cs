using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Plugins;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Plugins;

/// <summary>
/// 插件服务实现
/// Plugin service implementation
/// </summary>
public class PluginService : IPluginService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginService> _logger;
    private readonly ConcurrentDictionary<string, PluginMetadata> _pluginMetadata = new();
    private readonly ConcurrentDictionary<string, IPlugin> _loadedPlugins = new();
    private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
    private readonly string _pluginDirectory;

    public PluginService(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PluginService>();
        _pluginDirectory = Path.Combine(AppConstants.AppDataPath, "Plugins");

        // 确保插件目录存在
        if (!Directory.Exists(_pluginDirectory))
        {
            Directory.CreateDirectory(_pluginDirectory);
            _logger.LogInformation("Created plugin directory: {PluginDirectory}", _pluginDirectory);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginMetadata> LoadedPlugins =>
        _pluginMetadata.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;

    /// <inheritdoc />
    public async Task<int> DiscoverPluginsAsync(string pluginDirectory)
    {
        _logger.LogInformation("Discovering plugins in directory: {PluginDirectory}", pluginDirectory);

        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogWarning("Plugin directory does not exist: {PluginDirectory}", pluginDirectory);
            return 0;
        }

        var discoveredCount = 0;

        // 查找所有插件清单文件
        var manifestFiles = Directory.GetFiles(pluginDirectory, "plugin.json", SearchOption.AllDirectories);

        foreach (var manifestFile in manifestFiles)
        {
            try
            {
                var metadata = await LoadPluginMetadataAsync(manifestFile);
                if (metadata != null)
                {
                    _pluginMetadata.TryAdd(metadata.Id, metadata);
                    discoveredCount++;
                    _logger.LogInformation("Discovered plugin: {PluginName} ({PluginId}) v{Version}",
                        metadata.Name, metadata.Id, metadata.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering plugin from manifest: {ManifestFile}", manifestFile);
            }
        }

        _logger.LogInformation("Discovered {Count} plugins", discoveredCount);
        return discoveredCount;
    }

    /// <inheritdoc />
    public async Task<PluginLoadResult> LoadPluginAsync(string pluginId)
    {
        _logger.LogInformation("Loading plugin: {PluginId}", pluginId);

        if (!_pluginMetadata.TryGetValue(pluginId, out var metadata))
        {
            var errorMsg = $"Plugin not found: {pluginId}";
            _logger.LogError(errorMsg);
            return PluginLoadResult.CreateFailure(errorMsg);
        }

        if (_loadedPlugins.ContainsKey(pluginId))
        {
            _logger.LogWarning("Plugin already loaded: {PluginId}", pluginId);
            return PluginLoadResult.CreateSuccess(metadata, _loadedPlugins[pluginId]);
        }

        try
        {
            UpdatePluginState(metadata, PluginState.NotLoaded, PluginState.Loaded);

            // 检查依赖
            foreach (var dependency in metadata.Dependencies)
            {
                if (!_loadedPlugins.ContainsKey(dependency))
                {
                    _logger.LogWarning("Loading dependency: {Dependency}", dependency);
                    var depResult = await LoadPluginAsync(dependency);
                    if (!depResult.Success)
                    {
                        var errorMsg = $"Failed to load dependency: {dependency}";
                        UpdatePluginState(metadata, PluginState.Loaded, PluginState.Error, errorMsg);
                        return PluginLoadResult.CreateFailure(errorMsg);
                    }
                }
            }

            // 加载程序集
            var loadContext = new PluginAssemblyLoadContext(metadata.AssemblyPath);
            _loadContexts.TryAdd(pluginId, loadContext);

            var assembly = loadContext.LoadFromAssemblyPath(metadata.AssemblyPath);
            var pluginType = assembly.GetType(metadata.TypeFullName);

            if (pluginType == null)
            {
                var errorMsg = $"Plugin type not found: {metadata.TypeFullName}";
                UpdatePluginState(metadata, PluginState.Loaded, PluginState.Error, errorMsg);
                return PluginLoadResult.CreateFailure(errorMsg);
            }

            if (!typeof(IPlugin).IsAssignableFrom(pluginType))
            {
                var errorMsg = $"Type does not implement IPlugin: {metadata.TypeFullName}";
                UpdatePluginState(metadata, PluginState.Loaded, PluginState.Error, errorMsg);
                return PluginLoadResult.CreateFailure(errorMsg);
            }

            // 创建插件实例
            var plugin = Activator.CreateInstance(pluginType) as IPlugin;
            if (plugin == null)
            {
                var errorMsg = $"Failed to create plugin instance: {metadata.TypeFullName}";
                UpdatePluginState(metadata, PluginState.Loaded, PluginState.Error, errorMsg);
                return PluginLoadResult.CreateFailure(errorMsg);
            }

            _loadedPlugins.TryAdd(pluginId, plugin);

            // 初始化插件
            UpdatePluginState(metadata, PluginState.Loaded, PluginState.Initializing);
            var context = new PluginContext(
                _serviceProvider,
                _loggerFactory,
                Path.GetDirectoryName(metadata.AssemblyPath) ?? _pluginDirectory,
                AppConstants.AppDataPath);

            await plugin.InitializeAsync(context);

            UpdatePluginState(metadata, PluginState.Initializing, PluginState.Stopped);

            _logger.LogInformation("Successfully loaded plugin: {PluginName} ({PluginId})",
                metadata.Name, pluginId);

            return PluginLoadResult.CreateSuccess(metadata, plugin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin: {PluginId}", pluginId);
            UpdatePluginState(metadata, metadata.State, PluginState.Error, ex.Message);
            return PluginLoadResult.CreateFailure($"Error loading plugin: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        _logger.LogInformation("Unloading plugin: {PluginId}", pluginId);

        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            _logger.LogWarning("Plugin not loaded: {PluginId}", pluginId);
            return false;
        }

        if (!_pluginMetadata.TryGetValue(pluginId, out var metadata))
        {
            _logger.LogWarning("Plugin metadata not found: {PluginId}", pluginId);
            return false;
        }

        try
        {
            // 停止插件（如果正在运行）
            if (metadata.State == PluginState.Running)
            {
                await StopPluginAsync(pluginId);
            }

            // 释放插件资源
            await plugin.DisposeAsync();

            // 移除插件实例
            _loadedPlugins.TryRemove(pluginId, out _);

            // 卸载程序集上下文
            if (_loadContexts.TryRemove(pluginId, out var loadContext))
            {
                loadContext.Unload();
            }

            UpdatePluginState(metadata, metadata.State, PluginState.Unloaded);

            _logger.LogInformation("Successfully unloaded plugin: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin: {PluginId}", pluginId);
            UpdatePluginState(metadata, metadata.State, PluginState.Error, ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> StartPluginAsync(string pluginId)
    {
        _logger.LogInformation("Starting plugin: {PluginId}", pluginId);

        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            _logger.LogWarning("Plugin not loaded: {PluginId}", pluginId);
            return false;
        }

        if (!_pluginMetadata.TryGetValue(pluginId, out var metadata))
        {
            _logger.LogWarning("Plugin metadata not found: {PluginId}", pluginId);
            return false;
        }

        if (metadata.State == PluginState.Running)
        {
            _logger.LogWarning("Plugin already running: {PluginId}", pluginId);
            return true;
        }

        try
        {
            await plugin.StartAsync();
            
            // 如果是UI插件，注册页面到导航系统
            if (plugin is IUIPlugin uiPlugin)
            {
                var navService = _serviceProvider.GetService<IPluginNavigationService>();
                navService?.RegisterPluginPages(uiPlugin);
            }
            
            UpdatePluginState(metadata, metadata.State, PluginState.Running);
            _logger.LogInformation("Successfully started plugin: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting plugin: {PluginId}", pluginId);
            UpdatePluginState(metadata, metadata.State, PluginState.Error, ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> StopPluginAsync(string pluginId)
    {
        _logger.LogInformation("Stopping plugin: {PluginId}", pluginId);

        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            _logger.LogWarning("Plugin not loaded: {PluginId}", pluginId);
            return false;
        }

        if (!_pluginMetadata.TryGetValue(pluginId, out var metadata))
        {
            _logger.LogWarning("Plugin metadata not found: {PluginId}", pluginId);
            return false;
        }

        if (metadata.State != PluginState.Running)
        {
            _logger.LogWarning("Plugin not running: {PluginId}", pluginId);
            return true;
        }

        try
        {
            // 如果是UI插件，从导航系统注销页面
            if (plugin is IUIPlugin)
            {
                var navService = _serviceProvider.GetService<IPluginNavigationService>();
                navService?.UnregisterPluginPages(pluginId);
            }
            
            await plugin.StopAsync();
            UpdatePluginState(metadata, metadata.State, PluginState.Stopped);
            _logger.LogInformation("Successfully stopped plugin: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping plugin: {PluginId}", pluginId);
            UpdatePluginState(metadata, metadata.State, PluginState.Error, ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        if (!_pluginMetadata.TryGetValue(pluginId, out var metadata))
        {
            _logger.LogWarning("Plugin metadata not found: {PluginId}", pluginId);
            return false;
        }

        metadata.IsEnabled = true;
        await SavePluginMetadataAsync(metadata);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        if (!_pluginMetadata.TryGetValue(pluginId, out var metadata))
        {
            _logger.LogWarning("Plugin metadata not found: {PluginId}", pluginId);
            return false;
        }

        // 先停止插件
        if (metadata.State == PluginState.Running)
        {
            await StopPluginAsync(pluginId);
        }

        metadata.IsEnabled = false;
        await SavePluginMetadataAsync(metadata);
        return true;
    }

    /// <inheritdoc />
    public IPlugin? GetPlugin(string pluginId)
    {
        _loadedPlugins.TryGetValue(pluginId, out var plugin);
        return plugin;
    }

    /// <inheritdoc />
    public PluginMetadata? GetPluginMetadata(string pluginId)
    {
        _pluginMetadata.TryGetValue(pluginId, out var metadata);
        return metadata;
    }

    /// <inheritdoc />
    public IEnumerable<IUIPlugin> GetUIPlugins()
    {
        return _loadedPlugins.Values.OfType<IUIPlugin>();
    }

    private async Task<PluginMetadata?> LoadPluginMetadataAsync(string manifestPath)
    {
        var manifestContent = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestContent);

        if (manifest == null)
        {
            _logger.LogWarning("Failed to deserialize plugin manifest: {ManifestPath}", manifestPath);
            return null;
        }

        var pluginDirectory = Path.GetDirectoryName(manifestPath);
        if (string.IsNullOrEmpty(pluginDirectory))
        {
            _logger.LogWarning("Invalid manifest path: {ManifestPath}", manifestPath);
            return null;
        }

        var assemblyPath = Path.Combine(pluginDirectory, manifest.AssemblyFile);
        if (!File.Exists(assemblyPath))
        {
            _logger.LogWarning("Plugin assembly not found: {AssemblyPath}", assemblyPath);
            return null;
        }

        return new PluginMetadata
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description ?? string.Empty,
            Version = Version.Parse(manifest.Version),
            Author = manifest.Author ?? string.Empty,
            AssemblyPath = assemblyPath,
            TypeFullName = manifest.TypeFullName,
            Dependencies = manifest.Dependencies ?? [],
            MinAppVersion = !string.IsNullOrEmpty(manifest.MinAppVersion)
                ? Version.Parse(manifest.MinAppVersion)
                : null,
            Tags = manifest.Tags ?? []
        };
    }

    private async Task SavePluginMetadataAsync(PluginMetadata metadata)
    {
        var pluginDirectory = Path.GetDirectoryName(metadata.AssemblyPath);
        if (string.IsNullOrEmpty(pluginDirectory))
        {
            return;
        }

        var manifestPath = Path.Combine(pluginDirectory, "plugin.json");
        var manifest = new PluginManifest
        {
            Id = metadata.Id,
            Name = metadata.Name,
            Description = metadata.Description,
            Version = metadata.Version.ToString(),
            Author = metadata.Author,
            AssemblyFile = Path.GetFileName(metadata.AssemblyPath),
            TypeFullName = metadata.TypeFullName,
            Dependencies = metadata.Dependencies,
            MinAppVersion = metadata.MinAppVersion?.ToString(),
            Tags = metadata.Tags
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var manifestContent = JsonSerializer.Serialize(manifest, options);
        await File.WriteAllTextAsync(manifestPath, manifestContent);
    }

    private void UpdatePluginState(
        PluginMetadata metadata,
        PluginState oldState,
        PluginState newState,
        string? errorMessage = null)
    {
        metadata.State = newState;

        PluginStateChanged?.Invoke(this, new PluginStateChangedEventArgs
        {
            PluginId = metadata.Id,
            OldState = oldState,
            NewState = newState,
            ErrorMessage = errorMessage
        });
    }

    private class PluginManifest
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required string Version { get; set; }
        public string? Author { get; set; }
        public required string AssemblyFile { get; set; }
        public required string TypeFullName { get; set; }
        public List<string>? Dependencies { get; set; }
        public string? MinAppVersion { get; set; }
        public List<string>? Tags { get; set; }
    }
}
