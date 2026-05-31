using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Font option displayed by Designer v3 property grid.
/// </summary>
public sealed class FrontedFontFamilyOption
{
    /// <summary>
    /// Name shown in the ComboBox.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Stored layout value written back to FontFamily.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// WPF font used to preview this option.
    /// </summary>
    public FontFamily PreviewFontFamily { get; init; } = new("Arial");

    /// <summary>
    /// Whether this option comes from the bundled Assets/Fonts resources.
    /// </summary>
    public bool IsBuiltIn { get; init; }
}
