namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 显示的添加控件目录分组。
/// </summary>
public sealed class FrontedAddControlCatalogGroup
{
    /// <summary>
    /// 面向用户的分组显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 当此分组承载插件控件时的插件包标识。
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// 指示此分组是否承载插件控件。
    /// </summary>
    public bool IsPlugin { get; init; }

    /// <summary>
    /// 此分组中的控件项列表。
    /// </summary>
    public IReadOnlyList<FrontedAddControlCatalogItem> Items { get; init; } = [];
}
