using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Core.Plugins.Services;

namespace neo_bpsys_wpf.Plugins.Services;

/// <summary>
/// 插件配置服务实现
/// </summary>
public class PluginConfigurationService : IPluginConfigurationService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JsonElement>> _configurations = new();
    private readonly string _configDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PluginConfigurationService(string configDirectory)
    {
        _configDirectory = configDirectory;
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }
    }

    /// <inheritdoc/>
    public T GetValue<T>(string pluginId, string key, T defaultValue = default!)
    {
        var pluginConfig = GetPluginConfiguration(pluginId);
        
        if (pluginConfig.TryGetValue(key, out var value))
        {
            try
            {
                return value.Deserialize<T>(_jsonOptions) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
        
        return defaultValue;
    }

    /// <inheritdoc/>
    public void SetValue<T>(string pluginId, string key, T value)
    {
        var pluginConfig = GetPluginConfiguration(pluginId);
        var jsonElement = JsonSerializer.SerializeToElement(value, _jsonOptions);
        pluginConfig[key] = jsonElement;
    }

    /// <inheritdoc/>
    public bool HasKey(string pluginId, string key)
    {
        var pluginConfig = GetPluginConfiguration(pluginId);
        return pluginConfig.ContainsKey(key);
    }

    /// <inheritdoc/>
    public void RemoveKey(string pluginId, string key)
    {
        var pluginConfig = GetPluginConfiguration(pluginId);
        pluginConfig.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetKeys(string pluginId)
    {
        var pluginConfig = GetPluginConfiguration(pluginId);
        return pluginConfig.Keys.ToList();
    }

    /// <inheritdoc/>
    public void ClearPluginConfig(string pluginId)
    {
        if (_configurations.TryGetValue(pluginId, out var config))
        {
            config.Clear();
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (pluginId, config) in _configurations)
        {
            var filePath = GetConfigFilePath(pluginId);
            var configDict = config.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var json = JsonSerializer.Serialize(configDict, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_configDirectory))
            return;

        var configFiles = Directory.GetFiles(_configDirectory, "*.json");
        
        foreach (var filePath in configFiles)
        {
            try
            {
                var pluginId = Path.GetFileNameWithoutExtension(filePath);
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions);
                
                if (configDict != null)
                {
                    var concurrentDict = new ConcurrentDictionary<string, JsonElement>(configDict);
                    _configurations[pluginId] = concurrentDict;
                }
            }
            catch
            {
                // 忽略损坏的配置文件
            }
        }
    }

    private ConcurrentDictionary<string, JsonElement> GetPluginConfiguration(string pluginId)
    {
        return _configurations.GetOrAdd(pluginId, _ => new ConcurrentDictionary<string, JsonElement>());
    }

    private string GetConfigFilePath(string pluginId)
    {
        return Path.Combine(_configDirectory, $"{pluginId}.json");
    }
}
