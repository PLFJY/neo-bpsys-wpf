namespace neo_bpsys_wpf.Core.Plugins.Settings;

/// <summary>
/// 插件设置基类
/// </summary>
public abstract class PluginSettingsBase
{
    /// <summary>
    /// 设置版本，用于迁移
    /// </summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// 插件设置服务接口
/// </summary>
public interface IPluginSettingsService
{
    /// <summary>
    /// 获取插件设置
    /// </summary>
    /// <typeparam name="T">设置类型</typeparam>
    /// <param name="pluginId">插件ID</param>
    /// <returns>设置实例</returns>
    T GetSettings<T>(string pluginId) where T : PluginSettingsBase, new();

    /// <summary>
    /// 保存插件设置
    /// </summary>
    /// <typeparam name="T">设置类型</typeparam>
    /// <param name="pluginId">插件ID</param>
    /// <param name="settings">设置实例</param>
    void SaveSettings<T>(string pluginId, T settings) where T : PluginSettingsBase;

    /// <summary>
    /// 重置插件设置为默认值
    /// </summary>
    /// <typeparam name="T">设置类型</typeparam>
    /// <param name="pluginId">插件ID</param>
    /// <returns>默认设置实例</returns>
    T ResetSettings<T>(string pluginId) where T : PluginSettingsBase, new();

    /// <summary>
    /// 删除插件设置
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    void DeleteSettings(string pluginId);

    /// <summary>
    /// 检查插件是否有已保存的设置
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否存在设置</returns>
    bool HasSettings(string pluginId);

    /// <summary>
    /// 当设置变更时触发的事件
    /// </summary>
    event EventHandler<PluginSettingsChangedEventArgs>? SettingsChanged;
}

/// <summary>
/// 插件设置变更事件参数
/// </summary>
public sealed class PluginSettingsChangedEventArgs : EventArgs
{
    /// <summary>
    /// 插件ID
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// 旧设置
    /// </summary>
    public object? OldSettings { get; init; }

    /// <summary>
    /// 新设置
    /// </summary>
    public object? NewSettings { get; init; }
}
