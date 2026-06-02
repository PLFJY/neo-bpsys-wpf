using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Descriptor wrapper for an existing built-in fronted window.
/// </summary>
public sealed class FrontedBuiltInWindowDescriptor : IFrontedWindowDescriptor
{
    public string WindowId { get; init; } = string.Empty;

    public string WindowTypeName { get; init; } = string.Empty;

    public string FullWindowType => WindowTypeName;

    public string DisplayName { get; init; } = string.Empty;

    public string? DisplayNameKey { get; init; }

    public string? Description { get; init; }

    public string? DescriptionKey { get; init; }

    public FrontedWindowKind Kind => FrontedWindowKind.BuiltIn;

    public bool IsPlugin => false;

    public string? PackageId => null;

    public Type? WindowType { get; init; }

    public IReadOnlyList<FrontedCanvasDescriptor> Canvases { get; init; } = [];

    public static FrontedBuiltInWindowDescriptor FromInfo(FrontedWindowInfo info)
    {
        return new FrontedBuiltInWindowDescriptor
        {
            WindowId = info.Id,
            WindowTypeName = info.Name,
            DisplayName = info.Name,
            WindowType = info.WindowType,
            Canvases = info.Canvas.Select(canvas => new FrontedCanvasDescriptor
            {
                CanvasName = canvas.Name,
                DisplayName = string.IsNullOrWhiteSpace(canvas.DisplayName)
                    ? canvas.Name
                    : canvas.DisplayName.Trim(),
                Customizable = true
            }).ToArray()
        };
    }
}
