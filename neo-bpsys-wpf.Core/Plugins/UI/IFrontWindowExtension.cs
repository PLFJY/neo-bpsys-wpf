using System.Windows;

namespace neo_bpsys_wpf.Core.Plugins.UI;

/// <summary>
/// 前台窗口扩展点接口
/// </summary>
public interface IFrontWindowExtension : IUIExtensionPoint
{
    /// <summary>
    /// 窗口标题
    /// </summary>
    string Title { get; }

    /// <summary>
    /// 窗口宽度
    /// </summary>
    double Width { get; }

    /// <summary>
    /// 窗口高度
    /// </summary>
    double Height { get; }

    /// <summary>
    /// 是否允许调整大小
    /// </summary>
    bool AllowResize { get; }

    /// <summary>
    /// 是否显示在任务栏
    /// </summary>
    bool ShowInTaskbar { get; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    bool Topmost { get; }

    /// <summary>
    /// 创建窗口内容
    /// </summary>
    /// <returns>窗口内容元素</returns>
    FrameworkElement CreateWindowContent();

    /// <summary>
    /// 窗口样式（可选）
    /// </summary>
    Style? WindowStyle { get; }
}

/// <summary>
/// 前台窗口扩展点基类
/// </summary>
public abstract class FrontWindowExtensionBase : UIExtensionPointBase, IFrontWindowExtension
{
    /// <inheritdoc/>
    public override ExtensionPointLocation Location => ExtensionPointLocation.FrontWindowArea;

    /// <inheritdoc/>
    public abstract string Title { get; }

    /// <inheritdoc/>
    public virtual double Width => 400;

    /// <inheritdoc/>
    public virtual double Height => 300;

    /// <inheritdoc/>
    public virtual bool AllowResize => false;

    /// <inheritdoc/>
    public virtual bool ShowInTaskbar => false;

    /// <inheritdoc/>
    public virtual bool Topmost => true;

    /// <inheritdoc/>
    public abstract FrameworkElement CreateWindowContent();

    /// <inheritdoc/>
    public virtual Style? WindowStyle => null;

    /// <inheritdoc/>
    public override FrameworkElement CreateElement() => CreateWindowContent();
}
