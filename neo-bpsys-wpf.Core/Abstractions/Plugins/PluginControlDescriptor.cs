namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件控件描述符
/// Plugin control descriptor
/// </summary>
public class PluginControlDescriptor
{
    /// <summary>
    /// 控件类型
    /// Control type
    /// </summary>
    public required Type ControlType { get; init; }

    /// <summary>
    /// 控件名称
    /// Control name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 控件描述
    /// Control description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 控件分类
    /// Control category
    /// </summary>
    public string? Category { get; init; }
}
