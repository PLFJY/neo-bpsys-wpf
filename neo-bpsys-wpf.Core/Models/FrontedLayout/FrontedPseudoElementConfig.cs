using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Describes a generated visual part rendered inside a fronted control.
/// </summary>
public sealed class FrontedPseudoElementConfig
{
    /// <summary>
    /// Gets or sets the stable user-defined animation part name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated element kind.
    /// </summary>
    public FrontedPseudoElementKind Kind { get; set; } = FrontedPseudoElementKind.Rectangle;

    /// <summary>
    /// Gets or sets whether the part is rendered below or above the main content.
    /// </summary>
    public FrontedPseudoElementLayer Layer { get; set; } = FrontedPseudoElementLayer.AboveContent;

    /// <summary>
    /// Gets or sets the fixed width in pixels.
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// Gets or sets the fixed height in pixels.
    /// </summary>
    public double? Height { get; set; }

    /// <summary>
    /// Gets or sets an optional width expression such as <c>100%</c>.
    /// </summary>
    public string? WidthText { get; set; }

    /// <summary>
    /// Gets or sets an optional height expression such as <c>100%</c>.
    /// </summary>
    public string? HeightText { get; set; }

    /// <summary>
    /// Gets or sets the left offset relative to the parent control.
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// Gets or sets the top offset relative to the parent control.
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// Gets or sets the fill brush text.
    /// </summary>
    public string? Fill { get; set; }

    /// <summary>
    /// Gets or sets the stroke or border brush text.
    /// </summary>
    public string? Stroke { get; set; }

    /// <summary>
    /// Gets or sets the stroke or border thickness.
    /// </summary>
    public double StrokeThickness { get; set; }

    /// <summary>
    /// Gets or sets the image resource path used by image parts.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the initial opacity.
    /// </summary>
    public double Opacity { get; set; } = 1D;

    /// <summary>
    /// Gets or sets the initial WPF visibility name.
    /// </summary>
    public string Visibility { get; set; } = "Hidden";

    /// <summary>
    /// Gets or sets the layer-local z-index.
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Gets or sets whether the generated part participates in hit testing.
    /// </summary>
    public bool IsHitTestVisible { get; set; }
}

/// <summary>
/// Supported generated pseudo-element kinds.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedPseudoElementKind
{
    /// <summary>
    /// A filled and optionally stroked rectangle.
    /// </summary>
    Rectangle,

    /// <summary>
    /// A border element.
    /// </summary>
    Border,

    /// <summary>
    /// An image element.
    /// </summary>
    Image
}

/// <summary>
/// Visual layer used by a generated pseudo-element.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedPseudoElementLayer
{
    /// <summary>
    /// Renders behind the main control content.
    /// </summary>
    BelowContent,

    /// <summary>
    /// Renders above the main control content.
    /// </summary>
    AboveContent
}
