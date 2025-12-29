using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Loading;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件管理器实现
/// </summary>
public sealed class PluginManager : IPluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly IPluginLoader _pluginLoader;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginEventBus _eventBus;

    private readonly ConcurrentDictionary<string, IPlugin> _loadedPlugins = new();
    private readonly ConcurrentDictionary<string, PluginMetadata> _pluginMetadata = new();
    private readonly ConcurrentDictionary<string, PluginLoadContext> _pluginContexts = new();

    private readonly string _pluginStateFile;
    private readonly object _stateLock = new();

    /// <inheritdoc/>
    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins.Values.ToList();

    /// <inheritdoc/>
    public IReadOnlyList<PluginMetadata> AllPluginMetadata => _pluginMetadata.Values.ToList();

    /// <inheritdoc/>
    public event EventHandler<PluginLoadedEventArgs>? PluginLoaded;

    /// <inheritdoc/>
    public event EventHandler<PluginUnloadedEventArgs>? PluginUnloaded;

    /// <inheritdoc/>
    public event EventHandler<PluginStateChangedEventArgs>? PluginEnabled;

    /// <inheritdoc/>
    public event EventHandler<PluginStateChangedEventArgs>? PluginDisabled;

    /// <inheritdoc/>
    public event EventHandler<PluginLoadFailedEventArgs>? PluginLoadFailed;

    /// <summary>
    /// 创建插件管理器
    /// </summary>
    public PluginManager(
        ILogger<PluginManager> logger,
        IPluginLoader pluginLoader,
        IServiceProvider serviceProvider,
        IPluginEventBus eventBus)
    {
        _logger = logger;
        _pluginLoader = pluginLoader;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;

        // 插件状态保存路径
        _pluginStateFile = Path.Combine(AppConstants.UserDataPath, "plugin_states.json");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginMetadata>> DiscoverPluginsAsync(string pluginDirectory)
    {
        var discoveredPlugins = new List<PluginMetadata>();

        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogWarning("Plugin directory does not exist: {Directory}", pluginDirectory);
            return discoveredPlugins;
        }

        _logger.LogInformation("Discovering plugins in: {Directory}", pluginDirectory);

        // 查找所有可能的插件程序集
        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                // 检查是否有对应的插件清单文件
                var manifestPath = Path.ChangeExtension(pluginFile, ".plugin.json");
                if (File.Exists(manifestPath))
                {
                    var manifest = await LoadPluginManifestAsync(manifestPath);
                    if (manifest != null)
                    {
                        manifest.AssemblyPath = pluginFile;
                        discoveredPlugins.Add(manifest);
                        continue;
                    }
                }

                // 轻量级检查程序集是否包含插件
                if (_pluginLoader.IsValidPluginAssembly(pluginFile))
                {
                    // 尝试从程序集中提取元数据
                    var metadata = await ExtractMetadataFromAssemblyAsync(pluginFile);
                    if (metadata != null)
                    {
                        discoveredPlugins.Add(metadata);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover plugin: {File}", pluginFile);
            }
        }

        _logger.LogInformation("Discovered {Count} plugin(s)", discoveredPlugins.Count);
        return discoveredPlugins;
    }

    /// <inheritdoc/>
    public async Task<PluginLoadResult> LoadPluginAsync(string pluginPath)
    {
        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            return PluginLoadResult.Failed("Plugin path cannot be null or empty.");
        }

        if (!File.Exists(pluginPath))
        {
            return PluginLoadResult.Failed($"Plugin file not found: {pluginPath}");
        }

        _logger.LogInformation("Loading plugin from: {Path}", pluginPath);

        try
        {
            // 加载程序集
            var loadResult = _pluginLoader.LoadAssembly(pluginPath);
            if (!loadResult.Success)
            {
                RaisePluginLoadFailed(pluginPath, loadResult.ErrorMessage ?? "Unknown error", loadResult.Exception);
                return PluginLoadResult.Failed(loadResult.ErrorMessage ?? "Unknown error", loadResult.Exception);
            }

            var loadContextKey = loadResult.LoadContext?.PluginId;

            // 创建插件实例
            foreach (var pluginType in loadResult.PluginTypes)
            {
                try
                {
                    var plugin = CreatePluginInstance(pluginType);
                    if (plugin == null)
                    {
                        continue;
                    }

                    // 检查是否已加载同ID的插件
                    if (_loadedPlugins.ContainsKey(plugin.Id))
                    {
                        _logger.LogWarning("Plugin with ID '{PluginId}' is already loaded", plugin.Id);
                        continue;
                    }

                    // 检查依赖
                    var dependencyResult = CheckDependencies(plugin);
                    if (!dependencyResult.Success)
                    {
                        _logger.LogWarning("Plugin '{PluginId}' has unmet dependencies: {Dependencies}",
                            plugin.Id, string.Join(", ", dependencyResult.MissingDependencies));
                        continue;
                    }

                    // 创建元数据
                    var metadata = PluginMetadata.FromPlugin(plugin, pluginPath);
                    metadata.LoadState = PluginLoadState.Loading;

                    // 初始化插件
                    await plugin.InitializeAsync(_serviceProvider);

                    // 注册插件
                    _loadedPlugins[plugin.Id] = plugin;
                    _pluginMetadata[plugin.Id] = metadata;

                    if (loadResult.LoadContext != null)
                    {
                        _pluginContexts[plugin.Id] = loadResult.LoadContext;
                    }

                    metadata.LoadState = PluginLoadState.Loaded;

                    // 检查是否应该自动启用
                    var savedStates = LoadPluginStates();
                    if (!savedStates.TryGetValue(plugin.Id, out var isEnabled))
                    {
                        isEnabled = true; // 默认启用
                    }

                    if (isEnabled)
                    {
                        await plugin.EnableAsync();
                        metadata.IsEnabled = true;
                    }

                    // 触发事件
                    RaisePluginLoaded(plugin, metadata);

                    _logger.LogInformation("Successfully loaded plugin: {PluginId} v{Version}",
                        plugin.Id, plugin.Version);

                    return PluginLoadResult.Succeeded(metadata);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create plugin instance from type: {Type}", pluginType.FullName);
                }
            }

            // 如果程序集已加载但没有任何可用插件实例，必须卸载该程序集。
            // 否则会导致后续再次加载同一 DLL 时，PluginLoader 报："Plugin assembly already loaded"。
            if (!string.IsNullOrWhiteSpace(loadContextKey))
            {
                _pluginLoader.UnloadAssembly(loadContextKey);
            }

            return PluginLoadResult.Failed("No valid plugin instances could be created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin: {Path}", pluginPath);
            RaisePluginLoadFailed(pluginPath, ex.Message, ex);
            return PluginLoadResult.Failed(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PluginLoadResult>> LoadPluginsAsync(string pluginDirectory)
    {
        var results = new List<PluginLoadResult>();

        var plugins = await DiscoverPluginsAsync(pluginDirectory);

        // 按依赖顺序排序
        var sortedPlugins = SortPluginsByDependencies(plugins);

        foreach (var plugin in sortedPlugins)
        {
            var result = await LoadPluginAsync(plugin.AssemblyPath);
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            _logger.LogWarning("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        // 检查是否有其他插件依赖此插件
        var dependentPlugins = GetDependentPlugins(pluginId);
        if (dependentPlugins.Count > 0)
        {
            _logger.LogWarning("Cannot unload plugin '{PluginId}' because it is required by: {Dependents}",
                pluginId, string.Join(", ", dependentPlugins));
            return false;
        }

        try
        {
            // 禁用并卸载插件
            if (plugin is PluginBase pluginBase && pluginBase.IsEnabled)
            {
                await plugin.DisableAsync();
            }

            await plugin.UnloadAsync();

            // 移除注册
            _loadedPlugins.TryRemove(pluginId, out _);

            if (_pluginMetadata.TryRemove(pluginId, out var metadata))
            {
                metadata.LoadState = PluginLoadState.Unloaded;
            }

            // 卸载程序集
            if (_pluginContexts.TryRemove(pluginId, out var loadContext))
            {
                // PluginLoader 内部以“程序集文件名(不含扩展名)”作为 key（而不是 plugin.Id）。
                // 如果 plugin.Id 与程序集名不一致（例如 SamplePlugin -> com.example.sampleplugin），
                // 这里必须用加载上下文的 PluginId 才能真正卸载程序集，否则会残留，后续再次加载会报：
                // "Plugin assembly already loaded"。
                _pluginLoader.UnloadAssembly(loadContext.PluginId);
            }

            // 触发事件
            RaisePluginUnloaded(pluginId);
            await _eventBus.PublishAsync(new PluginUnloadedEvent { PluginId = pluginId });

            _logger.LogInformation("Successfully unloaded plugin: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload plugin: {PluginId}", pluginId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            _logger.LogWarning("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        // 幂等：已经启用则视为成功
        if (plugin is PluginBase pluginBase && pluginBase.IsEnabled)
        {
            return true;
        }

        try
        {
            await plugin.EnableAsync();

            if (_pluginMetadata.TryGetValue(pluginId, out var metadata))
            {
                metadata.IsEnabled = true;
                metadata.LoadError = null;
                metadata.LoadState = PluginLoadState.Loaded;
                RaisePluginEnabled(pluginId, metadata);
            }

            SavePluginState(pluginId, true);

            _logger.LogInformation("Enabled plugin: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable plugin: {PluginId}", pluginId);

            if (_pluginMetadata.TryGetValue(pluginId, out var metadata))
            {
                metadata.IsEnabled = false;
                metadata.LoadError = ex.Message;
                // 仍然保持为已加载状态，只是启用失败
                metadata.LoadState = PluginLoadState.Loaded;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            _logger.LogWarning("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        // 幂等：已经禁用则视为成功
        if (plugin is PluginBase pluginBase && !pluginBase.IsEnabled)
        {
            if (_pluginMetadata.TryGetValue(pluginId, out var metadata))
            {
                metadata.IsEnabled = false;
                metadata.LoadState = PluginLoadState.Disabled;
            }
            return true;
        }

        // 检查是否有其他启用的插件依赖此插件
        var dependentPlugins = GetDependentPlugins(pluginId)
            .Where(id => IsPluginEnabled(id))
            .ToList();

        if (dependentPlugins.Count > 0)
        {
            _logger.LogWarning("Cannot disable plugin '{PluginId}' because enabled plugins depend on it: {Dependents}",
                pluginId, string.Join(", ", dependentPlugins));

            if (_pluginMetadata.TryGetValue(pluginId, out var metadata))
            {
                metadata.LoadError = $"无法禁用：以下已启用插件依赖它：{string.Join(", ", dependentPlugins)}";
            }

            return false;
        }

        try
        {
            await plugin.DisableAsync();

            if (_pluginMetadata.TryGetValue(pluginId, out var metadata))
            {
                metadata.IsEnabled = false;
                metadata.LoadState = PluginLoadState.Disabled;
                metadata.LoadError = null;
                RaisePluginDisabled(pluginId, metadata);
            }

            SavePluginState(pluginId, false);

            _logger.LogInformation("Disabled plugin: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable plugin: {PluginId}", pluginId);

            if (_pluginMetadata.TryGetValue(pluginId, out var metadata))
            {
                metadata.IsEnabled = true;
                metadata.LoadError = ex.Message;
                metadata.LoadState = PluginLoadState.Loaded;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public IPlugin? GetPlugin(string pluginId)
    {
        return _loadedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }

    /// <inheritdoc/>
    public PluginMetadata? GetPluginMetadata(string pluginId)
    {
        return _pluginMetadata.TryGetValue(pluginId, out var metadata) ? metadata : null;
    }

    /// <inheritdoc/>
    public bool IsPluginLoaded(string pluginId)
    {
        return _loadedPlugins.ContainsKey(pluginId);
    }

    /// <inheritdoc/>
    public bool IsPluginEnabled(string pluginId)
    {
        return _pluginMetadata.TryGetValue(pluginId, out var metadata) && metadata.IsEnabled;
    }

    /// <inheritdoc/>
    public async Task<PluginLoadResult> ReloadPluginAsync(string pluginId)
    {
        var metadata = GetPluginMetadata(pluginId);
        if (metadata == null)
        {
            return PluginLoadResult.Failed($"Plugin not found: {pluginId}");
        }

        var assemblyPath = metadata.AssemblyPath;
        var wasEnabled = metadata.IsEnabled;

        // 卸载插件
        if (!await UnloadPluginAsync(pluginId))
        {
            return PluginLoadResult.Failed($"Failed to unload plugin: {pluginId}");
        }

        // 重新加载
        var result = await LoadPluginAsync(assemblyPath);

        // 恢复启用状态
        if (result.Success && wasEnabled)
        {
            // LoadPluginAsync 可能已经根据保存状态自动启用，避免二次 Enable。
            if (!IsPluginEnabled(pluginId))
            {
                await EnablePluginAsync(pluginId);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetPluginDependencies(string pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            return plugin.Dependencies.ToList();
        }

        if (_pluginMetadata.TryGetValue(pluginId, out var metadata))
        {
            return metadata.Dependencies.ToList();
        }

        return Array.Empty<string>();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetDependentPlugins(string pluginId)
    {
        var dependents = new List<string>();

        foreach (var (id, plugin) in _loadedPlugins)
        {
            if (plugin.Dependencies.Contains(pluginId))
            {
                dependents.Add(id);
            }
        }

        return dependents;
    }

    #region Private Methods

    private IPlugin? CreatePluginInstance(Type pluginType)
    {
        try
        {
            // 尝试使用DI容器创建实例
            var instance = ActivatorUtilities.CreateInstance(_serviceProvider, pluginType);
            return instance as IPlugin;
        }
        catch
        {
            // 回退到默认构造函数
            try
            {
                return Activator.CreateInstance(pluginType) as IPlugin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create plugin instance: {Type}", pluginType.FullName);
                return null;
            }
        }
    }

    private (bool Success, IReadOnlyList<string> MissingDependencies) CheckDependencies(IPlugin plugin)
    {
        var missingDependencies = new List<string>();

        foreach (var dependency in plugin.Dependencies)
        {
            if (!_loadedPlugins.ContainsKey(dependency))
            {
                missingDependencies.Add(dependency);
            }
        }

        return (missingDependencies.Count == 0, missingDependencies);
    }

    private IReadOnlyList<PluginMetadata> SortPluginsByDependencies(IReadOnlyList<PluginMetadata> plugins)
    {
        var sorted = new List<PluginMetadata>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(PluginMetadata plugin)
        {
            if (visited.Contains(plugin.Id))
                return;

            if (visiting.Contains(plugin.Id))
            {
                _logger.LogWarning("Circular dependency detected for plugin: {PluginId}", plugin.Id);
                return;
            }

            visiting.Add(plugin.Id);

            foreach (var depId in plugin.Dependencies)
            {
                var dep = plugins.FirstOrDefault(p => p.Id == depId);
                if (dep != null)
                {
                    Visit(dep);
                }
            }

            visiting.Remove(plugin.Id);
            visited.Add(plugin.Id);
            sorted.Add(plugin);
        }

        foreach (var plugin in plugins)
        {
            Visit(plugin);
        }

        return sorted;
    }

    private async Task<PluginMetadata?> LoadPluginManifestAsync(string manifestPath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest == null)
                return null;

            return new PluginMetadata
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = Version.Parse(manifest.Version),
                Description = manifest.Description ?? string.Empty,
                Author = manifest.Author ?? "Unknown",
                Dependencies = manifest.Dependencies ?? Array.Empty<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load plugin manifest: {Path}", manifestPath);
            return null;
        }
    }

    private async Task<PluginMetadata?> ExtractMetadataFromAssemblyAsync(string assemblyPath)
    {
        // 这里我们暂时创建一个基于文件名的元数据
        // 实际的元数据会在插件加载后从IPlugin接口获取
        await Task.CompletedTask;

        var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
        return new PluginMetadata
        {
            Id = fileName,
            Name = fileName,
            Version = new Version(1, 0, 0),
            AssemblyPath = assemblyPath
        };
    }

    private Dictionary<string, bool> LoadPluginStates()
    {
        lock (_stateLock)
        {
            try
            {
                if (File.Exists(_pluginStateFile))
                {
                    var json = File.ReadAllText(_pluginStateFile);
                    return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin states");
            }

            return new Dictionary<string, bool>();
        }
    }

    private void SavePluginState(string pluginId, bool isEnabled)
    {
        lock (_stateLock)
        {
            try
            {
                var states = LoadPluginStates();
                states[pluginId] = isEnabled;

                var directory = Path.GetDirectoryName(_pluginStateFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_pluginStateFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save plugin state for: {PluginId}", pluginId);
            }
        }
    }

    private void RaisePluginLoaded(IPlugin plugin, PluginMetadata metadata)
    {
        PluginLoaded?.Invoke(this, new PluginLoadedEventArgs
        {
            Plugin = plugin,
            Metadata = metadata
        });

        _eventBus.Publish(new PluginLoadedEvent
        {
            PluginMetadata = metadata
        });
    }

    private void RaisePluginUnloaded(string pluginId)
    {
        PluginUnloaded?.Invoke(this, new PluginUnloadedEventArgs
        {
            PluginId = pluginId
        });
    }

    private void RaisePluginEnabled(string pluginId, PluginMetadata metadata)
    {
        PluginEnabled?.Invoke(this, new PluginStateChangedEventArgs
        {
            PluginId = pluginId,
            Metadata = metadata
        });
    }

    private void RaisePluginDisabled(string pluginId, PluginMetadata metadata)
    {
        PluginDisabled?.Invoke(this, new PluginStateChangedEventArgs
        {
            PluginId = pluginId,
            Metadata = metadata
        });
    }

    private void RaisePluginLoadFailed(string pluginPath, string errorMessage, Exception? exception)
    {
        PluginLoadFailed?.Invoke(this, new PluginLoadFailedEventArgs
        {
            PluginPath = pluginPath,
            ErrorMessage = errorMessage,
            Exception = exception
        });
    }

    #endregion

    /// <summary>
    /// 插件清单文件模型
    /// </summary>
    private sealed class PluginManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string[]? Dependencies { get; set; }
    }
}
