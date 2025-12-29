using System.Windows;

namespace neo_bpsys_wpf.Core.Plugins.UI;

/// <summary>
/// UI扩展点枚举，定义插件可以注入UI组件的位置
/// </summary>
public enum UIExtensionPoint
{
    /// <summary>
    /// 主窗口工具栏
    /// </summary>
    MainToolbar,

    /// <summary>
    /// 主窗口菜单
    /// </summary>
    MainMenu,

    /// <summary>
    /// 主窗口状态栏
    /// </summary>
    StatusBar,

    /// <summary>
    /// 导航面板
    /// </summary>
    NavigationPanel,

    /// <summary>
    /// 主内容区域
    /// </summary>
    MainContent,

    /// <summary>
    /// 侧边栏
    /// </summary>
    Sidebar,

    /// <summary>
    /// 底部面板
    /// </summary>
    BottomPanel,

    /// <summary>
    /// BP窗口扩展区域
    /// </summary>
    BpWindowExtension,

    /// <summary>
    /// 比分窗口扩展区域
    /// </summary>
    ScoreWindowExtension,

    /// <summary>
    /// 设置页面扩展
    /// </summary>
    SettingsExtension,

    /// <summary>
    /// 自定义扩展点
    /// </summary>
    Custom
}

/// <summary>
/// UI组件注册信息
/// </summary>
public sealed class UIComponentRegistration
{
    /// <summary>
    /// 组件唯一标识符
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 组件显示名称
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// UI扩展点
    /// </summary>
    public required UIExtensionPoint ExtensionPoint { get; init; }

    /// <summary>
    /// 自定义扩展点名称（当ExtensionPoint为Custom时使用）
    /// </summary>
    public string? CustomExtensionPointName { get; init; }

    /// <summary>
    /// 组件工厂方法
    /// </summary>
    public required Func<IServiceProvider, FrameworkElement> ComponentFactory { get; init; }

    /// <summary>
    /// 组件排序优先级（数值越小越靠前）
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// 组件图标（可选）
    /// </summary>
    public object? Icon { get; init; }

    /// <summary>
    /// 组件工具提示
    /// </summary>
    public string? ToolTip { get; init; }

    /// <summary>
    /// 是否默认可见
    /// </summary>
    public bool IsVisibleByDefault { get; init; } = true;

    /// <summary>
    /// 所属插件ID
    /// </summary>
    public string? PluginId { get; internal set; }
}

/// <summary>
/// 导航页面注册信息
/// </summary>
public sealed class NavigationPageRegistration
{
    /// <summary>
    /// 页面唯一标识符
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 页面显示名称
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 页面类型
    /// </summary>
    public required Type PageType { get; init; }

    /// <summary>
    /// 视图模型类型（可选）
    /// </summary>
    public Type? ViewModelType { get; init; }

    /// <summary>
    /// 页面图标
    /// </summary>
    public object? Icon { get; init; }

    /// <summary>
    /// 页面排序优先级
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// 是否在导航菜单中显示
    /// </summary>
    public bool ShowInNavigation { get; init; } = true;

    /// <summary>
    /// 父级导航项ID（用于创建子菜单）
    /// </summary>
    public string? ParentId { get; init; }

    /// <summary>
    /// 所属插件ID
    /// </summary>
    public string? PluginId { get; internal set; }
}

/// <summary>
/// 窗口注册信息
/// </summary>
public sealed class WindowRegistration
{
    /// <summary>
    /// 窗口唯一标识符
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 窗口显示名称
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 窗口类型
    /// </summary>
    public required Type WindowType { get; init; }

    /// <summary>
    /// 视图模型类型（可选）
    /// </summary>
    public Type? ViewModelType { get; init; }

    /// <summary>
    /// 窗口工厂方法（可选，如果不提供则使用默认激活器）
    /// </summary>
    public Func<IServiceProvider, Window>? WindowFactory { get; init; }

    /// <summary>
    /// 是否为单例窗口
    /// </summary>
    public bool IsSingleton { get; init; } = true;

    /// <summary>
    /// 所属插件ID
    /// </summary>
    public string? PluginId { get; internal set; }
}
