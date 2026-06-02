namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 text-like control configuration that can receive legacy text styles.
/// </summary>
public interface IFrontedTextStyleConfig
{
    /// <summary>
    /// Font family.
    /// </summary>
    string? FontFamily { get; set; }

    /// <summary>
    /// Font weight.
    /// </summary>
    string? FontWeight { get; set; }

    /// <summary>
    /// Text color.
    /// </summary>
    string? Color { get; set; }

    /// <summary>
    /// Font size.
    /// </summary>
    double FontSize { get; set; }
}
