using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Services;

/// <summary>
/// 插件状态管理器，用于管理内置插件的启用/禁用状态
/// </summary>
public static class PluginStatusService
{
    private static PluginStatusConfig? _config;
    private static readonly string ConfigFilePath;

    static PluginStatusService()
    {
        // 确保应用数据目录存在
        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }

        ConfigFilePath = Path.Combine(AppConstants.AppDataPath, "PluginStatus.json");
        LoadConfig();
    }

    /// <summary>
    /// 获取插件是否启用的状态
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="isBuiltIn">是否是内置插件</param>
    /// <returns>如果插件启用返回true，否则返回false。如果未找到配置，内置插件默认返回false（禁用），外部插件默认返回true（启用）</returns>
    public static bool IsPluginEnabled(string pluginId, bool isBuiltIn)
    {
        if (_config?.PluginEnabledStatus != null
            && _config.PluginEnabledStatus.TryGetValue(pluginId, out var enabled))
        {
            return enabled;
        }
        
        // 默认情况下，内置插件不启用，外部插件启用
        return !isBuiltIn;
    }

    /// <summary>
    /// 设置插件的启用状态
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="enabled">是否启用</param>
    public static void SetPluginEnabled(string pluginId, bool enabled)
    {
        _config ??= new PluginStatusConfig();

        _config.PluginEnabledStatus[pluginId] = enabled;
        SaveConfig();
    }

    /// <summary>
    /// 加载插件状态配置
    /// </summary>
    private static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                _config = JsonSerializer.Deserialize<PluginStatusConfig>(json) ?? new PluginStatusConfig();
            }
            else
            {
                _config = new PluginStatusConfig();
                // 确保配置文件存在
                SaveConfig();
            }
        }
        catch (Exception)
        {
            // 如果加载失败，使用默认配置
            _config = new PluginStatusConfig();
        }
    }

    /// <summary>
    /// 获取所有插件的启用状态
    /// </summary>
    /// <returns>插件ID和启用状态的字典</returns>
    public static Dictionary<string, bool> GetAllPluginStatus()
    {
        return _config?.PluginEnabledStatus ?? new Dictionary<string, bool>();
    }

    /// <summary>
    /// 批量设置插件启用状态
    /// </summary>
    /// <param name="statuses">插件ID和启用状态的字典</param>
    public static void SetAllPluginStatus(Dictionary<string, bool> statuses)
    {
        _config ??= new PluginStatusConfig();

        _config.PluginEnabledStatus = statuses;
        SaveConfig();
    }

    /// <summary>
    /// 清除指定插件的状态配置
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    public static void ClearPluginStatus(string pluginId)
    {
        if (_config?.PluginEnabledStatus != null && _config.PluginEnabledStatus.Remove(pluginId))
        {
            SaveConfig();
        }
    }

    /// <summary>
    /// 保存插件状态配置
    /// </summary>
    private static void SaveConfig()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception)
        {
            // 记录错误但不抛出异常
        }
    }
}