using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Descriptor wrapper for an existing built-in WPF fronted window.
/// </summary>
/// <remarks>
/// Built-in <see cref="IFrontedWindowDescriptor.FullWindowType"/> values are the window type names,
/// for example <c>BpWindow</c>, <c>BpOverviewWindow</c>, or <c>ScoreGlobalWindow</c>.
/// </remarks>
public sealed class FrontedBuiltInWindowDescriptor : IFrontedWindowDescriptor
{
    /// <inheritdoc />
    public string WindowId { get; init; } = string.Empty;

    /// <inheritdoc />
    public string WindowTypeName { get; init; } = string.Empty;

    /// <inheritdoc />
    public string FullWindowType => WindowTypeName;

    /// <inheritdoc />
    public string DisplayName { get; init; } = string.Empty;

    /// <inheritdoc />
    public string? DisplayNameKey { get; init; }

    /// <inheritdoc />
    public string? Description { get; init; }

    /// <inheritdoc />
    public string? DescriptionKey { get; init; }

    /// <inheritdoc />
    public string? GroupKey { get; init; }

    /// <inheritdoc />
    public int? DisplayOrder { get; init; }

    /// <inheritdoc />
    public bool IsVisibleInFrontManage { get; init; } = true;

    /// <inheritdoc />
    public bool IsV3LayoutWindow { get; init; }

    /// <inheritdoc />
    public bool Customizable { get; init; } = true;

    /// <inheritdoc />
    public FrontedWindowKind Kind => FrontedWindowKind.BuiltIn;

    /// <inheritdoc />
    public bool IsPlugin => false;

    /// <inheritdoc />
    public string? PackageId => null;

    /// <summary>
    /// Concrete WPF window type registered by <see cref="Attributes.FrontedWindowInfo"/>.
    /// </summary>
    public Type? WindowType { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<FrontedCanvasDescriptor> Canvases { get; init; } = [];

    /// <summary>
    /// Creates a descriptor from the existing built-in window attribute metadata.
    /// </summary>
    public static FrontedBuiltInWindowDescriptor FromInfo(FrontedWindowInfo info)
    {
        return new FrontedBuiltInWindowDescriptor
        {
            WindowId = info.Id,
            WindowTypeName = info.Name,
            DisplayName = info.Name,
            DisplayNameKey = $"Designer.Window.{info.Name}",
            GroupKey = "BuiltIn",
            DisplayOrder = GetBuiltInDisplayOrder(info.Name),
            WindowType = info.WindowType,
            IsV3LayoutWindow = false,
            Canvases = [new FrontedCanvasDescriptor
            {
                CanvasName = FrontedLayoutConstants.BaseCanvasName,
                DisplayName = FrontedLayoutConstants.BaseCanvasName,
                Customizable = true
            }]
        };
    }

    private static int GetBuiltInDisplayOrder(string windowTypeName)
    {
        return Enum.TryParse<FrontedWindowType>(windowTypeName, ignoreCase: false, out var windowType)
            ? (int)windowType * 100
            : int.MaxValue;
    }
}
