namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件元数据，用于描述插件的基本信息
/// </summary>
public sealed class PluginMetadata
{
    /// <summary>
    /// 插件唯一标识符
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 插件名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 插件版本
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// 插件描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 插件作者
    /// </summary>
    public string Author { get; init; } = "Unknown";

    /// <summary>
    /// 插件依赖的其他插件ID列表
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 插件程序集路径
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// 插件是否已启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 插件加载状态
    /// </summary>
    public PluginLoadState LoadState { get; set; } = PluginLoadState.NotLoaded;

    /// <summary>
    /// 加载错误信息（如果加载失败）
    /// </summary>
    public string? LoadError { get; set; }

    /// <summary>
    /// 从IPlugin实例创建元数据
    /// </summary>
    /// <param name="plugin">插件实例</param>
    /// <param name="assemblyPath">程序集路径</param>
    /// <returns>插件元数据</returns>
    public static PluginMetadata FromPlugin(IPlugin plugin, string assemblyPath)
    {
        return new PluginMetadata
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Version = plugin.Version,
            Description = plugin.Description,
            Author = plugin.Author,
            Dependencies = plugin.Dependencies,
            AssemblyPath = assemblyPath
        };
    }
}

/// <summary>
/// 插件加载状态
/// </summary>
public enum PluginLoadState
{
    /// <summary>
    /// 未加载
    /// </summary>
    NotLoaded,

    /// <summary>
    /// 正在加载
    /// </summary>
    Loading,

    /// <summary>
    /// 已加载
    /// </summary>
    Loaded,

    /// <summary>
    /// 加载失败
    /// </summary>
    Failed,

    /// <summary>
    /// 已禁用
    /// </summary>
    Disabled,

    /// <summary>
    /// 已卸载
    /// </summary>
    Unloaded
}
