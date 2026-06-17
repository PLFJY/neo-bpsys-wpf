namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Describes a font file stored inside the active fronted layout package.
/// </summary>
public sealed class FrontedPackageFontItem
{
    /// <summary>
    /// Gets or sets the font file name under <c>resources/fonts</c>.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute font file path.
    /// </summary>
    public string PhysicalPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the package font family names discovered from the file.
    /// </summary>
    public IReadOnlyList<string> FontFamilyNames { get; set; } = [];

    /// <summary>
    /// Gets the display text for font families in this file.
    /// </summary>
    public string FontFamilyDisplayName => FontFamilyNames.Count == 0
        ? string.Empty
        : string.Join(", ", FontFamilyNames);

    /// <summary>
    /// Gets or sets the package font resource URIs produced by the file.
    /// </summary>
    public IReadOnlyList<string> ResourceUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of current layout string values that reference the file.
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// Gets whether the font file is referenced by the current layout package.
    /// </summary>
    public bool IsReferenced => ReferenceCount > 0;

    /// <summary>
    /// Gets whether the font file can be deleted without breaking an existing layout reference.
    /// </summary>
    public bool CanDelete => !IsReferenced;
}
