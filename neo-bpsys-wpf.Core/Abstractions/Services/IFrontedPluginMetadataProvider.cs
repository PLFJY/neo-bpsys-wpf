namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides loaded plugin metadata for fronted layout dependency synchronization.
/// </summary>
public interface IFrontedPluginMetadataProvider
{
    /// <summary>
    /// 判断指定插件是否已安装。
    /// </summary>
    /// <param name="packageId">插件包 ID。</param>
    /// <returns>如果已安装则返回 <c>true</c>。</returns>
    bool IsPluginInstalled(string packageId);

    /// <summary>
    /// 尝试获取指定插件的版本号。
    /// </summary>
    /// <param name="packageId">插件包 ID。</param>
    /// <param name="version">输出参数，插件版本号。</param>
    /// <returns>如果找到则返回 <c>true</c>。</returns>
    bool TryGetPluginVersion(string packageId, out string version);

    /// <summary>
    /// 尝试获取指定插件的显示名称。
    /// </summary>
    /// <param name="packageId">插件包 ID。</param>
    /// <param name="displayName">输出参数，插件显示名称。</param>
    /// <returns>如果找到则返回 <c>true</c>。</returns>
    bool TryGetPluginDisplayName(string packageId, out string displayName);

    /// <summary>
    /// 尝试获取指定插件的安装目录。
    /// </summary>
    /// <param name="packageId">插件包 ID。</param>
    /// <param name="folder">输出参数，插件目录路径。</param>
    /// <returns>如果找到则返回 <c>true</c>。</returns>
    bool TryGetPluginFolder(string packageId, out string folder);
}
