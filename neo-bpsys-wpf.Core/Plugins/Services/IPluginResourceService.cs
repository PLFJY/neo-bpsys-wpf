namespace neo_bpsys_wpf.Core.Plugins.Services;

/// <summary>
/// 插件资源服务接口
/// </summary>
public interface IPluginResourceService
{
    /// <summary>
    /// 获取插件资源目录路径
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>资源目录路径</returns>
    string GetResourceDirectory(string pluginId);

    /// <summary>
    /// 获取插件数据目录路径
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>数据目录路径</returns>
    string GetDataDirectory(string pluginId);

    /// <summary>
    /// 获取插件配置文件路径
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>配置文件路径</returns>
    string GetConfigFilePath(string pluginId);

    /// <summary>
    /// 读取插件资源文件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="relativePath">相对路径</param>
    /// <returns>文件内容</returns>
    Task<byte[]> ReadResourceAsync(string pluginId, string relativePath);

    /// <summary>
    /// 读取插件资源文件为文本
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="relativePath">相对路径</param>
    /// <returns>文件文本内容</returns>
    Task<string> ReadResourceTextAsync(string pluginId, string relativePath);

    /// <summary>
    /// 写入插件数据文件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="relativePath">相对路径</param>
    /// <param name="data">文件数据</param>
    Task WriteDataAsync(string pluginId, string relativePath, byte[] data);

    /// <summary>
    /// 写入插件数据文件为文本
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="relativePath">相对路径</param>
    /// <param name="text">文件文本内容</param>
    Task WriteDataTextAsync(string pluginId, string relativePath, string text);

    /// <summary>
    /// 检查资源文件是否存在
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="relativePath">相对路径</param>
    /// <returns>是否存在</returns>
    bool ResourceExists(string pluginId, string relativePath);

    /// <summary>
    /// 检查数据文件是否存在
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="relativePath">相对路径</param>
    /// <returns>是否存在</returns>
    bool DataExists(string pluginId, string relativePath);
}
