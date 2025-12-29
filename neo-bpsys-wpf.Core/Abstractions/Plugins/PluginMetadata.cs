namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件元数据
/// Plugin metadata
/// </summary>
public class PluginMetadata
{
    /// <summary>
    /// 插件唯一标识符
    /// Unique plugin identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 插件名称
    /// Plugin name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 插件描述
    /// Plugin description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 插件版本
    /// Plugin version
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// 插件作者
    /// Plugin author
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// 插件程序集路径
    /// Plugin assembly path
    /// </summary>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// 插件类型全名
    /// Plugin type full name
    /// </summary>
    public required string TypeFullName { get; init; }

    /// <summary>
    /// 插件当前状态
    /// Current plugin state
    /// </summary>
    public PluginState State { get; set; } = PluginState.NotLoaded;

    /// <summary>
    /// 是否启用
    /// Whether plugin is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 依赖的插件ID列表
    /// List of dependent plugin IDs
    /// </summary>
    public List<string> Dependencies { get; init; } = [];

    /// <summary>
    /// 插件最低需要的应用程序版本
    /// Minimum required application version
    /// </summary>
    public Version? MinAppVersion { get; init; }

    /// <summary>
    /// 插件标签
    /// Plugin tags
    /// </summary>
    public List<string> Tags { get; init; } = [];
}
