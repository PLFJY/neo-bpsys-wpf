namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 内置或插件前台控件的添加控件目录条目。
/// </summary>
public sealed class FrontedAddControlCatalogItem
{
    /// <summary>
    /// 控件类型标识。
    /// </summary>
    public string ControlType { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的描述。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 可选的图标键。
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 指示此控件是否来自插件。
    /// </summary>
    public bool IsPlugin { get; init; }

    /// <summary>
    /// 当 <see cref="IsPlugin"/> 为 true 时的插件包标识。
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// 当 <see cref="IsPlugin"/> 为 true 时的插件显示名称。
    /// </summary>
    public string? PluginDisplayName { get; init; }

    /// <summary>
    /// 指示该控件当前是否可添加。
    /// </summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>
    /// 控件不可用时的人类可读原因。
    /// </summary>
    public string? UnavailableReason { get; init; }
}
