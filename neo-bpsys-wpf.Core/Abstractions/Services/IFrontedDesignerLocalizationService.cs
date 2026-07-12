namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 为设计器 v3 提供显示层的本地化，且不改变布局契约。
/// </summary>
public interface IFrontedDesignerLocalizationService
{
    /// <summary>
    /// 获取属性的显示名称。
    /// </summary>
    /// <param name="propertyName">属性名。</param>
    /// <returns>属性的本地化显示名称。</returns>
    string GetPropertyDisplayName(string propertyName);

    /// <summary>
    /// 获取属性的描述文本。
    /// </summary>
    /// <param name="propertyName">属性名。</param>
    /// <returns>属性的本地化描述。</returns>
    string GetPropertyDescription(string propertyName);

    /// <summary>
    /// 获取属性分组的显示名称。
    /// </summary>
    /// <param name="groupName">分组名。</param>
    /// <returns>分组的本地化显示名称。</returns>
    string GetGroupDisplayName(string groupName);

    /// <summary>
    /// 获取控件类型的显示名称。
    /// </summary>
    /// <param name="controlType">控件类型名。</param>
    /// <returns>控件类型的本地化显示名称。</returns>
    string GetControlTypeDisplayName(string controlType);

    /// <summary>
    /// 获取前台窗口的显示名称。
    /// </summary>
    /// <param name="windowTypeName">窗口类型名。</param>
    /// <returns>窗口的本地化显示名称。</returns>
    string GetWindowDisplayName(string windowTypeName);

    /// <summary>
    /// 获取 Canvas 的显示名称。
    /// </summary>
    /// <param name="canvasName">Canvas 名称。</param>
    /// <returns>Canvas 的本地化显示名称。</returns>
    string GetCanvasDisplayName(string canvasName);

    /// <summary>
    /// 获取属性选项值的显示名称。
    /// </summary>
    /// <param name="propertyName">属性名。</param>
    /// <param name="value">属性值。</param>
    /// <returns>选项值的本地化显示名称。</returns>
    string GetOptionDisplayName(string propertyName, object? value);

    /// <summary>
    /// 获取绑定节点的显示名称。
    /// </summary>
    /// <param name="pathOrPropertyName">绑定路径或属性名。</param>
    /// <param name="fullPath">完整路径（可选）。</param>
    /// <returns>绑定节点的本地化显示名称。</returns>
    string GetBindingNodeDisplayName(string pathOrPropertyName, string? fullPath = null);

    /// <summary>
    /// 获取绑定类型的显示名称。
    /// </summary>
    /// <param name="typeName">类型名。</param>
    /// <returns>绑定类型的本地化显示名称。</returns>
    string GetBindingTypeDisplayName(string typeName);

    /// <summary>
    /// 获取设计器相关的本地化文本。
    /// </summary>
    /// <param name="key">本地化键。</param>
    /// <param name="fallback">未找到时的回退文本。</param>
    /// <returns>本地化后的文本。</returns>
    string GetDesignerText(string key, string fallback);
}
