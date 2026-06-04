using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Controls when an image overlay is visible.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedOverlayVisibilityMode
{
    /// <summary>
    /// Visible when the bound value is true.
    /// </summary>
    VisibleWhenTrue,

    /// <summary>
    /// Visible when the bound value is false.
    /// </summary>
    VisibleWhenFalse,

    /// <summary>
    /// Always visible.
    /// </summary>
    Always
}
