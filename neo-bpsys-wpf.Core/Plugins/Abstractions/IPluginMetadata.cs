namespace neo_bpsys_wpf.Core.Plugins.Abstractions;

/// <summary>
/// 插件元数据，描述插件的基本信息
/// </summary>
public interface IPluginMetadata
{
    /// <summary>
    /// 插件唯一标识符
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 插件显示名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// 插件作者
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 插件依赖的其他插件ID列表
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    /// 最低支持的宿主应用程序版本
    /// </summary>
    Version? MinimumHostVersion { get; }

    /// <summary>
    /// 插件图标路径（可选）
    /// </summary>
    string? IconPath { get; }

    /// <summary>
    /// 插件主页URL（可选）
    /// </summary>
    string? HomepageUrl { get; }
}
