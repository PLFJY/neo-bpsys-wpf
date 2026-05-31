using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Resolves editor bounds for designer hitboxes and adorners.
/// </summary>
public static class FrontedDesignerBoundsResolver
{
    /// <summary>
    /// Resolves width and height for a rendered design item.
    /// </summary>
    public static FrontedDesignerResolvedBounds Resolve(
        FrontedControlConfigBase config,
        double? actualWidth = null,
        double? actualHeight = null)
    {
        var width = config.Width
            ?? GetPositiveActualSize(actualWidth)
            ?? FrontedDesignerGeometryHelper.MinHitWidth;
        var height = config.Height
            ?? GetPositiveActualSize(actualHeight)
            ?? FrontedDesignerGeometryHelper.MinHitHeight;

        return new FrontedDesignerResolvedBounds(config.Left, config.Top, width, height);
    }

    private static double? GetPositiveActualSize(double? value)
    {
        return value is > 0D && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value)
            ? value.Value
            : null;
    }
}

/// <summary>
/// Resolved editor bounds in logical Canvas coordinates.
/// </summary>
/// <param name="Left">Canvas left.</param>
/// <param name="Top">Canvas top.</param>
/// <param name="Width">Resolved width.</param>
/// <param name="Height">Resolved height.</param>
public readonly record struct FrontedDesignerResolvedBounds(
    double Left,
    double Top,
    double Width,
    double Height);
