namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件页面描述符
/// Plugin page descriptor
/// </summary>
public class PluginPageDescriptor
{
    /// <summary>
    /// 页面类型
    /// Page type
    /// </summary>
    public required Type PageType { get; init; }

    /// <summary>
    /// 视图模型类型
    /// ViewModel type
    /// </summary>
    public Type? ViewModelType { get; init; }

    /// <summary>
    /// 页面标题
    /// Page title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 页面图标 (使用 SymbolRegular 枚举)
    /// Page icon (using SymbolRegular enum)
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 导航路由
    /// Navigation route
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// 是否在导航菜单中显示
    /// Whether to show in navigation menu
    /// </summary>
    public bool ShowInNavigation { get; init; } = true;

    /// <summary>
    /// 菜单排序优先级
    /// Menu sort priority
    /// </summary>
    public int Priority { get; init; } = 100;
}
