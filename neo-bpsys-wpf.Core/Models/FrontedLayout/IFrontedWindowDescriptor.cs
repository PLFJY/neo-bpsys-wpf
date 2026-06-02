namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Common v3 descriptor for built-in and plugin fronted windows.
/// </summary>
public interface IFrontedWindowDescriptor
{
    string WindowId { get; }

    string WindowTypeName { get; }

    string FullWindowType { get; }

    string DisplayName { get; }

    string? DisplayNameKey { get; }

    string? Description { get; }

    string? DescriptionKey { get; }

    FrontedWindowKind Kind { get; }

    bool IsPlugin { get; }

    string? PackageId { get; }

    IReadOnlyList<FrontedCanvasDescriptor> Canvases { get; }
}
