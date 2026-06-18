using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Services.Abstractions;

/// <summary>
/// 插件安装服务接口。
/// </summary>
public interface IPluginInstallService
{
    /// <summary>
    /// 从已解压的目录安装插件。
    /// </summary>
    /// <param name="extractedDirectoryPath">已解压的插件目录路径。</param>
    /// <returns>安装结果。</returns>
    PluginInstallResult InstallFromExtractedDirectory(string extractedDirectoryPath);

    /// <summary>
    /// 从插件归档文件安装插件。
    /// </summary>
    /// <param name="archivePath">插件归档文件路径。</param>
    /// <param name="extractedDirectoryPath">临时解压目录路径。</param>
    /// <returns>安装结果。</returns>
    PluginInstallResult InstallFromArchive(string archivePath, string extractedDirectoryPath);
}

/// <summary>
/// 插件安装结果。
/// </summary>
public sealed class PluginInstallResult
{
    /// <summary>
    /// 插件清单。
    /// </summary>
    public required PluginManifest Manifest { get; init; }

    /// <summary>
    /// 是否为更新安装。
    /// </summary>
    public bool IsUpdate { get; init; }

    /// <summary>
    /// 是否需要重启应用。
    /// </summary>
    public bool RestartRequired { get; init; }
}
