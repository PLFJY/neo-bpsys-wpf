using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Core.Attributes;

/// <summary>
/// 后台页面信息
/// </summary>
/// <param name="id">页面ID</param>
/// <param name="name">页面名称</param>
/// <param name="icon">页面图标</param>
/// <param name="category"></param>
[AttributeUsage(AttributeTargets.Class)]
public class BackendPageInfo(
    string id,
    string name,
    SymbolRegular icon = SymbolRegular.Person532,
    BackendPageCategory category = BackendPageCategory.External)
    : Attribute
{
    /// <summary>
    /// 页面名称
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 页面ID
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// 页面图标
    /// </summary>
    public SymbolRegular Icon { get; } = icon;

    /// <summary>
    /// 页面类别
    /// </summary>
    public BackendPageCategory Category { get; } = category;

    /// <summary>
    /// 页面类型
    /// </summary>
    public Type? PageType { get; internal set; }
}