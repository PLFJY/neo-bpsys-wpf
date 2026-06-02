namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Describes a WPF fronted output window known to the v3 fronted window registry.
/// </summary>
/// <remarks>
/// <see cref="WindowId"/> is the runtime identity and should be a stable GUID.
/// <see cref="FullWindowType"/> is the layout/package identity. Built-in windows use names such as
/// <c>BpWindow</c> or <c>ScoreGlobalWindow</c>; plugin windows use
/// <c>plugin:{PackageId}/{WindowTypeName}</c>.
/// </remarks>
public interface IFrontedWindowDescriptor
{
    /// <summary>
    /// Stable runtime window identity. Plugin authors should generate one GUID and keep it unchanged.
    /// </summary>
    string WindowId { get; }

    /// <summary>
    /// Short window type name inside its provider, such as <c>BpWindow</c> or a plugin's local window name.
    /// </summary>
    string WindowTypeName { get; }

    /// <summary>
    /// Layout and package identity. Plugin values use <c>plugin:{PackageId}/{WindowTypeName}</c>.
    /// </summary>
    string FullWindowType { get; }

    /// <summary>
    /// Fallback display name used when <see cref="DisplayNameKey"/> is not localized by the host.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Optional localization key for the window display name.
    /// </summary>
    string? DisplayNameKey { get; }

    /// <summary>
    /// Fallback human-readable description.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Optional localization key for the window description.
    /// </summary>
    string? DescriptionKey { get; }

    /// <summary>
    /// Provider and editing mode for this fronted window.
    /// </summary>
    FrontedWindowKind Kind { get; }

    /// <summary>
    /// Whether the descriptor came from a plugin contributor.
    /// </summary>
    bool IsPlugin { get; }

    /// <summary>
    /// Plugin package id when <see cref="IsPlugin"/> is true; otherwise <see langword="null"/>.
    /// </summary>
    string? PackageId { get; }

    /// <summary>
    /// Canvases exposed by this window. Only customizable canvases participate in Designer v3 layout editing.
    /// </summary>
    IReadOnlyList<FrontedCanvasDescriptor> Canvases { get; }
}
