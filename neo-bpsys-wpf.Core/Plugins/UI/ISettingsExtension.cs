using System.Windows;

namespace neo_bpsys_wpf.Core.Plugins.UI;

/// <summary>
/// 设置页面扩展点接口
/// </summary>
public interface ISettingsExtension : IUIExtensionPoint
{
    /// <summary>
    /// 设置分组名称
    /// </summary>
    string GroupName { get; }

    /// <summary>
    /// 设置项标题
    /// </summary>
    string Title { get; }

    /// <summary>
    /// 设置项描述
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 设置项图标
    /// </summary>
    object? Icon { get; }

    /// <summary>
    /// 加载设置
    /// </summary>
    Task LoadSettingsAsync();

    /// <summary>
    /// 保存设置
    /// </summary>
    Task SaveSettingsAsync();

    /// <summary>
    /// 重置为默认设置
    /// </summary>
    Task ResetToDefaultAsync();
}

/// <summary>
/// 设置页面扩展点基类
/// </summary>
public abstract class SettingsExtensionBase : UIExtensionPointBase, ISettingsExtension
{
    /// <inheritdoc/>
    public override ExtensionPointLocation Location => ExtensionPointLocation.SettingsPage;

    /// <inheritdoc/>
    public virtual string GroupName => "插件设置";

    /// <inheritdoc/>
    public abstract string Title { get; }

    /// <inheritdoc/>
    public virtual string? Description => null;

    /// <inheritdoc/>
    public virtual object? Icon => null;

    /// <inheritdoc/>
    public abstract Task LoadSettingsAsync();

    /// <inheritdoc/>
    public abstract Task SaveSettingsAsync();

    /// <inheritdoc/>
    public virtual Task ResetToDefaultAsync() => Task.CompletedTask;
}
