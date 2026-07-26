using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 资源浏览器显示的资源项。
/// </summary>
public sealed class FrontedResourceBrowserItem
{
    /// <summary>
    /// 显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 选中后写入的路径值。
    /// </summary>
    public string SelectedPath { get; init; } = string.Empty;

    /// <summary>
    /// 文件路径。
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// 资源分类。
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 来源显示名称。
    /// </summary>
    public string SourceDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 类型显示名称。
    /// </summary>
    public string TypeDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 来源与类型的组合显示名称。
    /// </summary>
    public string SourceAndTypeDisplayName =>
        string.IsNullOrWhiteSpace(TypeDisplayName)
            ? SourceDisplayName
            : $"{SourceDisplayName} / {TypeDisplayName}";

    /// <summary>
    /// 缩略图。
    /// </summary>
    public ImageSource? Thumbnail { get; init; }

    /// <summary>
    /// 是否为绝对路径文件。
    /// </summary>
    public bool IsAbsoluteFile { get; init; }
}
