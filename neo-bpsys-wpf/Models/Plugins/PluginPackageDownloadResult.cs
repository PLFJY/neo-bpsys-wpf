namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>
/// 表示一个已经下载并解压完成、可继续安装的插件包。
/// </summary>
public class PluginPackageDownloadResult
{
    /// <summary>
    /// 插件包解压后的目录路径。
    /// </summary>
    public string ExtractedDirectoryPath { get; init; } = string.Empty;
}
