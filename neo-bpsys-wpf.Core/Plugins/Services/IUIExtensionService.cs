using neo_bpsys_wpf.Core.Plugins.UI;

namespace neo_bpsys_wpf.Core.Plugins.Services;

/// <summary>
/// UI扩展服务接口
/// </summary>
public interface IUIExtensionService
{
    /// <summary>
    /// 注册UI扩展点
    /// </summary>
    /// <param name="extension">扩展点实例</param>
    void RegisterExtension(IUIExtensionPoint extension);

    /// <summary>
    /// 注销UI扩展点
    /// </summary>
    /// <param name="extensionId">扩展点ID</param>
    void UnregisterExtension(string extensionId);

    /// <summary>
    /// 获取指定位置的所有扩展点
    /// </summary>
    /// <param name="location">扩展点位置</param>
    /// <returns>扩展点列表（按优先级排序）</returns>
    IReadOnlyList<IUIExtensionPoint> GetExtensions(ExtensionPointLocation location);

    /// <summary>
    /// 获取指定自定义位置的所有扩展点
    /// </summary>
    /// <param name="customLocationName">自定义位置名称</param>
    /// <returns>扩展点列表（按优先级排序）</returns>
    IReadOnlyList<IUIExtensionPoint> GetExtensions(string customLocationName);

    /// <summary>
    /// 获取指定类型的扩展点
    /// </summary>
    /// <typeparam name="T">扩展点类型</typeparam>
    /// <returns>扩展点列表</returns>
    IReadOnlyList<T> GetExtensions<T>() where T : IUIExtensionPoint;

    /// <summary>
    /// 获取指定ID的扩展点
    /// </summary>
    /// <param name="extensionId">扩展点ID</param>
    /// <returns>扩展点实例</returns>
    IUIExtensionPoint? GetExtension(string extensionId);

    /// <summary>
    /// 扩展点变更事件
    /// </summary>
    event EventHandler<UIExtensionChangedEventArgs>? ExtensionChanged;
}

/// <summary>
/// UI扩展点变更事件参数
/// </summary>
public class UIExtensionChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变更类型
    /// </summary>
    public required UIExtensionChangeType ChangeType { get; init; }

    /// <summary>
    /// 相关扩展点
    /// </summary>
    public required IUIExtensionPoint Extension { get; init; }
}

/// <summary>
/// UI扩展点变更类型
/// </summary>
public enum UIExtensionChangeType
{
    /// <summary>
    /// 已添加
    /// </summary>
    Added,

    /// <summary>
    /// 已移除
    /// </summary>
    Removed,

    /// <summary>
    /// 已更新
    /// </summary>
    Updated
}
