using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Plugins.UI;

/// <summary>
/// 导航页面扩展点接口
/// </summary>
public interface INavigationPageExtension : IUIExtensionPoint
{
    /// <summary>
    /// 页面标题
    /// </summary>
    string Title { get; }

    /// <summary>
    /// 页面图标（支持Symbol或ImageSource）
    /// </summary>
    object? Icon { get; }

    /// <summary>
    /// 页面描述
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 导航页面类型
    /// </summary>
    Type PageType { get; }

    /// <summary>
    /// 创建页面实例
    /// </summary>
    /// <returns>页面实例</returns>
    object CreatePageInstance();

    /// <summary>
    /// 是否在导航菜单中显示
    /// </summary>
    bool ShowInNavigation { get; }
}

/// <summary>
/// 导航页面扩展点基类
/// </summary>
public abstract class NavigationPageExtensionBase : UIExtensionPointBase, INavigationPageExtension
{
    /// <inheritdoc/>
    public override ExtensionPointLocation Location => ExtensionPointLocation.NavigationMenu;

    /// <inheritdoc/>
    public abstract string Title { get; }

    /// <inheritdoc/>
    public virtual object? Icon => null;

    /// <inheritdoc/>
    public virtual string? Description => null;

    /// <inheritdoc/>
    public abstract Type PageType { get; }

    /// <inheritdoc/>
    public virtual object CreatePageInstance()
    {
        // 默认实现：使用 Activator 创建无参构造函数的实例
        return Activator.CreateInstance(PageType) 
            ?? throw new InvalidOperationException($"Failed to create instance of {PageType}");
    }

    /// <inheritdoc/>
    public virtual bool ShowInNavigation => true;

    /// <inheritdoc/>
    public override System.Windows.FrameworkElement CreateElement()
    {
        // 导航页面的实际创建由宿主应用处理
        return new ContentControl();
    }
}
