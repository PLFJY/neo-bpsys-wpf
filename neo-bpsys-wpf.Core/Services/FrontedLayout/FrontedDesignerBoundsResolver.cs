using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 解析设计器命中框和装饰器的编辑器边界。
/// </summary>
public static class FrontedDesignerBoundsResolver
{
    /// <summary>
    /// 解析已渲染设计项的宽度和高度。
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
/// 逻辑画布坐标中已解析的编辑器边界。
/// </summary>
/// <param name="Left">画布左侧。</param>
/// <param name="Top">画布顶部。</param>
/// <param name="Width">已解析的宽度。</param>
/// <param name="Height">已解析的高度。</param>
public readonly record struct FrontedDesignerResolvedBounds(
    double Left,
    double Top,
    double Width,
    double Height);
