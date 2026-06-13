using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Describes an optional visual effect that can be applied to generated visual elements.
/// </summary>
public sealed class FrontedVisualEffectConfig
{
    /// <summary>
    /// Gets or sets the effect kind.
    /// </summary>
    public FrontedVisualEffectKind Kind { get; set; } = FrontedVisualEffectKind.None;

    /// <summary>
    /// Gets or sets the effect color text.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the effect opacity.
    /// </summary>
    public double Opacity { get; set; } = 1D;

    /// <summary>
    /// Gets or sets the effect blur radius.
    /// </summary>
    public double BlurRadius { get; set; }

    /// <summary>
    /// Gets or sets the drop shadow depth.
    /// </summary>
    public double ShadowDepth { get; set; }

    /// <summary>
    /// Gets or sets the drop shadow direction.
    /// </summary>
    public double Direction { get; set; }
}

/// <summary>
/// Supported visual effect kinds.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedVisualEffectKind
{
    /// <summary>
    /// No visual effect.
    /// </summary>
    None,

    /// <summary>
    /// Glow effect implemented with a zero-depth drop shadow.
    /// </summary>
    Glow,

    /// <summary>
    /// WPF drop shadow effect.
    /// </summary>
    DropShadow
}
