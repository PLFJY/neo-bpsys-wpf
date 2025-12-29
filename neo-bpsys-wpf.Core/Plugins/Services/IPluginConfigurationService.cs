namespace neo_bpsys_wpf.Core.Plugins.Services;

/// <summary>
/// 插件配置服务接口
/// </summary>
public interface IPluginConfigurationService
{
    /// <summary>
    /// 获取配置值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="pluginId">插件ID</param>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>配置值</returns>
    T GetValue<T>(string pluginId, string key, T defaultValue = default!);

    /// <summary>
    /// 设置配置值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="pluginId">插件ID</param>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    void SetValue<T>(string pluginId, string key, T value);

    /// <summary>
    /// 检查配置键是否存在
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="key">配置键</param>
    /// <returns>是否存在</returns>
    bool HasKey(string pluginId, string key);

    /// <summary>
    /// 删除配置键
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="key">配置键</param>
    void RemoveKey(string pluginId, string key);

    /// <summary>
    /// 获取插件的所有配置键
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>配置键列表</returns>
    IReadOnlyList<string> GetKeys(string pluginId);

    /// <summary>
    /// 清除插件的所有配置
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    void ClearPluginConfig(string pluginId);

    /// <summary>
    /// 保存配置到持久化存储
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 从持久化存储加载配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
