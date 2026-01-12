namespace neo_bpsys_wpf.Core.Models;

public record PluginManifest
{
    /// <summary>
    /// 入口程序集。加载插件时，将在此入口程序集中搜索插件类。
    /// </summary>
    /// <example>MyPlugin.dll</example>
    public string EntranceAssembly { get; set; } = string.Empty;

    /// <summary>
    /// 插件显示名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 插件ID。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 插件描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 插件图标路径。默认为icon.png。
    /// </summary>
    public string Icon { get; set; } = "icon.png";

    /// <summary>
    /// 项目 Url
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 插件版本
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// 插件目标主程序版本
    /// </summary>
    public string ApiVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// 插件作者
    /// </summary>
    public string Author { get; set; } = string.Empty;
}