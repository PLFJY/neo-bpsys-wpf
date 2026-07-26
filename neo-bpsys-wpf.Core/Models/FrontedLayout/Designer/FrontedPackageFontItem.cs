namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 描述存储在活动前台布局包内的字体文件。
/// </summary>
public sealed class FrontedPackageFontItem
{
    /// <summary>
    /// 获取或设置 <c>resources/fonts</c> 下的字体文件名。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置字体文件的绝对路径。
    /// </summary>
    public string PhysicalPath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置从该文件中发现的包字体家族名称。
    /// </summary>
    public IReadOnlyList<string> FontFamilyNames { get; set; } = [];

    /// <summary>
    /// 获取此文件中字体家族的显示文本。
    /// </summary>
    public string FontFamilyDisplayName => FontFamilyNames.Count == 0
        ? string.Empty
        : string.Join(", ", FontFamilyNames);

    /// <summary>
    /// 获取或设置由该文件产生的包字体资源 URI。
    /// </summary>
    public IReadOnlyList<string> ResourceUris { get; set; } = [];

    /// <summary>
    /// 获取或设置引用此文件的当前布局字符串值的数量。
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// 获取指示该字体文件是否被当前布局包引用的值。
    /// </summary>
    public bool IsReferenced => ReferenceCount > 0;

    /// <summary>
    /// 获取指示该字体文件是否可在不破坏现有布局引用的情况下删除的值。
    /// </summary>
    public bool CanDelete => !IsReferenced;
}
