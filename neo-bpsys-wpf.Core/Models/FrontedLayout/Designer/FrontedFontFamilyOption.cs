using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 属性网格显示的字体选项。
/// </summary>
public sealed class FrontedFontFamilyOption
{
    /// <summary>
    /// 在下拉框中显示的名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 写回到 FontFamily 的存储布局值。
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// 用于预览此选项的 WPF 字体。
    /// </summary>
    public FontFamily PreviewFontFamily { get; init; } = new("Arial");

    /// <summary>
    /// 指示此选项是否来自内置的 Assets/Fonts 资源。
    /// </summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// 指示此选项是否来自活动布局包。
    /// </summary>
    public bool IsPackageFont { get; init; }

    /// <summary>
    /// 在字体名称旁显示的可选短徽章文本。
    /// </summary>
    public string BadgeText { get; init; } = string.Empty;
}
