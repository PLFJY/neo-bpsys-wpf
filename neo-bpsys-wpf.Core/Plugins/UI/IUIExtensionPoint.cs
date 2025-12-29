using System.Windows;

namespace neo_bpsys_wpf.Core.Plugins.UI;

/// <summary>
/// UI扩展点位置枚举
/// </summary>
public enum ExtensionPointLocation
{
    /// <summary>
    /// 主窗口工具栏
    /// </summary>
    MainWindowToolbar,

    /// <summary>
    /// 主窗口状态栏
    /// </summary>
    MainWindowStatusBar,

    /// <summary>
    /// 导航菜单
    /// </summary>
    NavigationMenu,

    /// <summary>
    /// 设置页面
    /// </summary>
    SettingsPage,

    /// <summary>
    /// 前台窗口区域
    /// </summary>
    FrontWindowArea,

    /// <summary>
    /// BP窗口区域
    /// </summary>
    BpWindowArea,

    /// <summary>
    /// 比分窗口区域
    /// </summary>
    ScoreWindowArea,

    /// <summary>
    /// 上下文菜单
    /// </summary>
    ContextMenu,

    /// <summary>
    /// 自定义位置
    /// </summary>
    Custom
}

/// <summary>
/// UI扩展点接口
/// </summary>
public interface IUIExtensionPoint
{
    /// <summary>
    /// 扩展点唯一标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 扩展点位置
    /// </summary>
    ExtensionPointLocation Location { get; }

    /// <summary>
    /// 自定义位置名称（当Location为Custom时使用）
    /// </summary>
    string? CustomLocationName { get; }

    /// <summary>
    /// 排序优先级（数值越小越靠前）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 创建UI元素
    /// </summary>
    /// <returns>UI元素</returns>
    FrameworkElement CreateElement();

    /// <summary>
    /// 扩展点是否可见
    /// </summary>
    bool IsVisible { get; }
}

/// <summary>
/// UI扩展点基类
/// </summary>
public abstract class UIExtensionPointBase : IUIExtensionPoint
{
    /// <inheritdoc/>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract ExtensionPointLocation Location { get; }

    /// <inheritdoc/>
    public virtual string? CustomLocationName => null;

    /// <inheritdoc/>
    public virtual int Priority => 100;

    /// <inheritdoc/>
    public abstract FrameworkElement CreateElement();

    /// <inheritdoc/>
    public virtual bool IsVisible => true;
}
