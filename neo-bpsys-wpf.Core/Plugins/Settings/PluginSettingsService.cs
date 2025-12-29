using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.Core.Plugins.Settings;

/// <summary>
/// 插件设置服务实现
/// </summary>
public sealed class PluginSettingsService : IPluginSettingsService
{
    private readonly ILogger<PluginSettingsService> _logger;
    private readonly string _settingsDirectory;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public event EventHandler<PluginSettingsChangedEventArgs>? SettingsChanged;

    /// <summary>
    /// 创建插件设置服务
    /// </summary>
    public PluginSettingsService(ILogger<PluginSettingsService> logger)
    {
        _logger = logger;
        _settingsDirectory = Path.Combine(AppConstants.UserDataPath, "PluginSettings");

        if (!Directory.Exists(_settingsDirectory))
        {
            Directory.CreateDirectory(_settingsDirectory);
        }
    }

    /// <inheritdoc/>
    public T GetSettings<T>(string pluginId) where T : PluginSettingsBase, new()
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));
        }

        var settingsPath = GetSettingsPath(pluginId);

        lock (_lock)
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<T>(json, GetJsonOptions());
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load settings for plugin: {PluginId}", pluginId);
            }

            // 返回默认设置
            return new T();
        }
    }

    /// <inheritdoc/>
    public void SaveSettings<T>(string pluginId, T settings) where T : PluginSettingsBase
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));
        }

        ArgumentNullException.ThrowIfNull(settings);

        var settingsPath = GetSettingsPath(pluginId);
        object? oldSettings = null;

        lock (_lock)
        {
            try
            {
                // 读取旧设置
                if (File.Exists(settingsPath))
                {
                    var oldJson = File.ReadAllText(settingsPath);
                    oldSettings = JsonSerializer.Deserialize<T>(oldJson, GetJsonOptions());
                }

                // 保存新设置
                var json = JsonSerializer.Serialize(settings, GetJsonOptions());
                File.WriteAllText(settingsPath, json);

                _logger.LogInformation("Saved settings for plugin: {PluginId}", pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings for plugin: {PluginId}", pluginId);
                throw;
            }
        }

        // 触发事件
        SettingsChanged?.Invoke(this, new PluginSettingsChangedEventArgs
        {
            PluginId = pluginId,
            OldSettings = oldSettings,
            NewSettings = settings
        });
    }

    /// <inheritdoc/>
    public T ResetSettings<T>(string pluginId) where T : PluginSettingsBase, new()
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));
        }

        var defaultSettings = new T();
        SaveSettings(pluginId, defaultSettings);
        return defaultSettings;
    }

    /// <inheritdoc/>
    public void DeleteSettings(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null or empty.", nameof(pluginId));
        }

        var settingsPath = GetSettingsPath(pluginId);

        lock (_lock)
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    File.Delete(settingsPath);
                    _logger.LogInformation("Deleted settings for plugin: {PluginId}", pluginId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete settings for plugin: {PluginId}", pluginId);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public bool HasSettings(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        return File.Exists(GetSettingsPath(pluginId));
    }

    private string GetSettingsPath(string pluginId)
    {
        // 清理插件ID以确保是有效的文件名
        var safePluginId = string.Join("_", pluginId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_settingsDirectory, $"{safePluginId}.json");
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
