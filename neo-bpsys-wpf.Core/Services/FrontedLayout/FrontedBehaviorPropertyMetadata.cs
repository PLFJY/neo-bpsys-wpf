using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 为编辑器和验证器提供通用的行为图属性元数据。
/// </summary>
public static class FrontedBehaviorPropertyMetadata
{
    private static readonly IReadOnlyDictionary<string, FrontedAnimatablePropertyMetadata> Metadata =
        CreateMetadata().ToDictionary(item => item.PropertyName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 内置行为编辑器公开的通用可动画属性名称。
    /// </summary>
    public static IReadOnlyList<string> CommonPropertyNames { get; } =
    [
        "Opacity",
        "Visibility",
        "VisualOffsetX",
        "VisualOffsetY",
        "ClipInsetLeft",
        "ClipInsetTop",
        "ClipInsetRight",
        "ClipInsetBottom",
        "ScaleX",
        "ScaleY",
        "Rotation",
        "Width",
        "Height",
        "FillColor",
        "StrokeColor",
        "StrokeThickness",
        "TextColor",
        "Foreground",
        "FontSize",
        "TintColor",
        "TintStrength",
        "TextureStrength"
        ,"GaussianBlurRadius"
    ];

    /// <summary>
    /// 应用于生成的控件根的属性名称。
    /// </summary>
    public static IReadOnlyList<string> ControlLayerPropertyNames { get; } =
    [
        "Opacity",
        "Visibility",
        "VisualOffsetX",
        "VisualOffsetY",
        "ClipInsetLeft",
        "ClipInsetTop",
        "ClipInsetRight",
        "ClipInsetBottom",
        "ScaleX",
        "ScaleY",
        "Rotation",
        "Width",
        "Height",
        "TintColor",
        "TintStrength",
        "TextureStrength"
        ,"GaussianBlurRadius"
    ];

    /// <summary>
    /// 通常应用于目标控件主要内容的属性名称。
    /// </summary>
    public static IReadOnlyList<string> ContentLayerPropertyNames { get; } =
    [
        "Opacity",
        "Visibility",
        "VisualOffsetX",
        "VisualOffsetY",
        "ClipInsetLeft",
        "ClipInsetTop",
        "ClipInsetRight",
        "ClipInsetBottom",
        "ScaleX",
        "ScaleY",
        "Rotation",
        "Width",
        "Height",
        "FillColor",
        "StrokeColor",
        "StrokeThickness",
        "TextColor",
        "Foreground",
        "FontSize"
        ,"GaussianBlurRadius"
    ];

    /// <summary>
    /// 应用于运行时矩形覆盖层的属性名称。
    /// </summary>
    public static IReadOnlyList<string> OverlayLayerPropertyNames { get; } =
    [
        "Opacity",
        "Visibility",
        "VisualOffsetX",
        "VisualOffsetY",
        "ScaleX",
        "ScaleY",
        "Rotation",
        "Width",
        "Height",
        "FillColor",
        "StrokeColor",
        "StrokeThickness"
        ,"GaussianBlurRadius"
    ];

    /// <summary>
    /// 获取为目标图层推荐的属性名称。
    /// </summary>
    /// <param name="targetLayer">目标图层。</param>
    /// <param name="includeAll">是否包含全部重置的哨兵值。</param>
    /// <returns>该图层推荐的属性名称。</returns>
    public static IReadOnlyList<string> GetPropertyNamesForLayer(FrontedAnimationTargetLayer targetLayer, bool includeAll = false)
    {
        var properties = targetLayer switch
        {
            FrontedAnimationTargetLayer.Control => ControlLayerPropertyNames,
            FrontedAnimationTargetLayer.Content => ContentLayerPropertyNames,
            FrontedAnimationTargetLayer.OverlayAbove or FrontedAnimationTargetLayer.OverlayBelow => OverlayLayerPropertyNames,
            _ => CommonPropertyNames
        };

        return includeAll ? ["All", .. properties] : properties;
    }

    /// <summary>
    /// WPF 行为动画支持的可见性值。
    /// </summary>
    public static IReadOnlyList<string> VisibilityOptions { get; } = ["Visible", "Hidden", "Collapsed"];

    /// <summary>
    /// 获取受支持的可动画属性的元数据。
    /// </summary>
    /// <param name="propertyName">稳定的属性名称。</param>
    /// <returns>属性元数据；当属性未知时返回 <c>null</c>。</returns>
    public static FrontedAnimatablePropertyMetadata? Find(string? propertyName) =>
        string.IsNullOrWhiteSpace(propertyName) ? null : Metadata.GetValueOrDefault(propertyName);

    /// <summary>
    /// 确定属性名称是否表示数值型行为值。
    /// </summary>
    /// <param name="propertyName">要检查的属性名称。</param>
    /// <returns>当属性为数值型时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsNumericProperty(string? propertyName) =>
        Is(propertyName,
            "Opacity",
            "VisualOffsetX",
            "VisualOffsetY",
            "ClipInsetLeft",
            "ClipInsetTop",
            "ClipInsetRight",
            "ClipInsetBottom",
            "ScaleX",
            "ScaleY",
            "Rotation",
            "Width",
            "Height",
            "StrokeThickness",
            "FontSize",
            "TintStrength",
            "TextureStrength",
            "GaussianBlurRadius",
            "DurationMs");

    /// <summary>
    /// 确定属性名称是否表示颜色型行为值。
    /// </summary>
    /// <param name="propertyName">要检查的属性名称。</param>
    /// <returns>当属性为颜色型时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsColorProperty(string? propertyName) =>
        Is(propertyName, "FillColor", "StrokeColor", "TextColor", "TintColor", "Foreground", "Background");

    /// <summary>
    /// 确定属性名称是否表示可见性值。
    /// </summary>
    /// <param name="propertyName">要检查的属性名称。</param>
    /// <returns>当属性为 Visibility 时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsVisibilityProperty(string? propertyName) => Is(propertyName, "Visibility");

    /// <summary>
    /// 根据指定的行为属性名称验证值。
    /// </summary>
    /// <param name="propertyName">行为属性名称。</param>
    /// <param name="value">要验证的文本值。</param>
    /// <param name="message">验证失败时的验证消息。</param>
    /// <returns>当值对该属性有效时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool TryValidateValue(string? propertyName, string? value, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(propertyName) || string.Equals(propertyName, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value?.TrimStart().StartsWith('=') == true)
        {
            if (!IsNumericProperty(propertyName))
            {
                message = $"{propertyName} does not accept a numeric expression.";
                return false;
            }

            // Runtime validates expressions with the real event context. Keeping malformed
            // expressions non-fatal here lets the graph continue after skipping only that action.
            return true;
        }

        if (IsVisibilityProperty(propertyName))
        {
            if (VisibilityOptions.Any(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            message = "Visibility must be Visible, Hidden, or Collapsed.";
            return false;
        }

        if (IsColorProperty(propertyName))
        {
            if (ColorHelper.TryParseColor(value, out _))
            {
                return true;
            }

            message = "Color must be #RRGGBB, #AARRGGBB, or a WPF color name.";
            return false;
        }

        if (IsNumericProperty(propertyName))
        {
            var numericText = SupportsPercentage(propertyName) && value?.Trim().EndsWith('%') == true
                ? value.Trim()[..^1]
                : value;
            if (!double.TryParse(numericText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
                || !double.IsFinite(number))
            {
                message = $"{propertyName} must be a finite number.";
                return false;
            }

            if (Is(propertyName, "Opacity", "TintStrength", "TextureStrength") && number is < 0D or > 1D)
            {
                message = $"{propertyName} must be between 0 and 1.";
                return false;
            }

            if (Is(propertyName, "Width", "Height", "StrokeThickness", "FontSize", "DurationMs", "GaussianBlurRadius") && number < 0D)
            {
                message = $"{propertyName} must be greater than or equal to 0.";
                return false;
            }

            if (Is(propertyName, "ScaleX", "ScaleY") && number <= 0D)
            {
                message = $"{propertyName} should be greater than 0.";
                return false;
            }
        }

        return true;
    }

    private static bool Is(string? propertyName, params string[] candidates) =>
        candidates.Any(candidate => string.Equals(candidate, propertyName, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<FrontedAnimatablePropertyMetadata> CreateMetadata() =>
    [
        Numeric("Opacity", "0.0 - 1.0", "0.25", 0, 1),
        new("Visibility", "enum", "Visible / Hidden / Collapsed", "Visible", "Designer.Graph.PropertyHint.Visibility", allowedValues: VisibilityOptions),
        Numeric("VisualOffsetX", "pixels, e.g. 20 or -20", "20"),
        Numeric("VisualOffsetY", "pixels, e.g. 20 or -20", "-20"),
        Numeric("ClipInsetLeft", "pixels or %, e.g. 20 or 100%", "100%"),
        Numeric("ClipInsetTop", "pixels or %, e.g. 20 or 100%", "100%"),
        Numeric("ClipInsetRight", "pixels or %, e.g. 20 or 100%", "100%"),
        Numeric("ClipInsetBottom", "pixels or %, e.g. 20 or 100%", "100%"),
        Numeric("ScaleX", "1 = normal size, 1.1 = 110%", "1.1", double.Epsilon),
        Numeric("ScaleY", "1 = normal size, 1.1 = 110%", "1.1", double.Epsilon),
        Numeric("Rotation", "degrees, e.g. 15 or -15", "15"),
        Numeric("Width", "pixels, e.g. 120", "120", 0),
        Numeric("Height", "pixels, e.g. 120", "120", 0),
        Color("TintColor"),
        Color("FillColor"),
        Color("StrokeColor"),
        Color("TextColor"),
        Color("Foreground"),
        Numeric("StrokeThickness", "pixels, e.g. 2", "2", 0),
        Numeric("TintStrength", "0.0 - 1.0", "0.5", 0, 1),
        Numeric("TextureStrength", "0.0 - 1.0", "0.5", 0, 1),
        Numeric("FontSize", "e.g. 24", "24", 0)
        ,Numeric("GaussianBlurRadius", "pixels, e.g. 12", "12", 0)
    ];

    private static FrontedAnimatablePropertyMetadata Numeric(
        string name,
        string placeholder,
        string example,
        double? min = null,
        double? max = null) =>
        new(name, "double", placeholder, example, $"Designer.Graph.PropertyHint.{name}", min, max);

    private static FrontedAnimatablePropertyMetadata Color(string name) =>
        new(name, "color", "#AARRGGBB or #RRGGBB", "#FFFFFFFF", $"Designer.Graph.PropertyHint.{name}");

    /// <summary>
    /// 确定指定属性是否接受百分比值，如 <c>100%</c>。
    /// </summary>
    /// <param name="propertyName">要检查的属性名称。</param>
    /// <returns>当支持百分比值时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool SupportsPercentage(string? propertyName) =>
        Is(
            propertyName,
            "VisualOffsetX",
            "VisualOffsetY",
            "ClipInsetLeft",
            "ClipInsetTop",
            "ClipInsetRight",
            "ClipInsetBottom");
}

/// <summary>
/// 描述可动画属性的编辑器指导和约束。
/// </summary>
public sealed class FrontedAnimatablePropertyMetadata
{
    /// <summary>
    /// 初始化新的属性元数据实例。
    /// </summary>
    /// <param name="propertyName">稳定的属性名称。</param>
    /// <param name="typeName">稳定的值类型名称。</param>
    /// <param name="placeholder">回退输入占位符。</param>
    /// <param name="example">示例值。</param>
    /// <param name="descriptionKey">上下文帮助的本地化键。</param>
    /// <param name="min">可选的最小数值。</param>
    /// <param name="max">可选的最大数值。</param>
    /// <param name="allowedValues">可选的稳定允许值。</param>
    public FrontedAnimatablePropertyMetadata(
        string propertyName,
        string typeName,
        string placeholder,
        string example,
        string descriptionKey,
        double? min = null,
        double? max = null,
        IReadOnlyList<string>? allowedValues = null)
    {
        PropertyName = propertyName;
        TypeName = typeName;
        Placeholder = placeholder;
        Example = example;
        DescriptionKey = descriptionKey;
        Min = min;
        Max = max;
        AllowedValues = allowedValues;
    }

    /// <summary>获取稳定的属性名称。</summary>
    public string PropertyName { get; }

    /// <summary>获取稳定的值类型名称。</summary>
    public string TypeName { get; }

    /// <summary>获取编辑器回退占位符。</summary>
    public string Placeholder { get; }

    /// <summary>获取示例值。</summary>
    public string Example { get; }

    /// <summary>获取上下文帮助的本地化键。</summary>
    public string DescriptionKey { get; }

    /// <summary>获取可选的最小数值。</summary>
    public double? Min { get; }

    /// <summary>获取可选的最大数值。</summary>
    public double? Max { get; }

    /// <summary>获取可选的稳定允许值。</summary>
    public IReadOnlyList<string>? AllowedValues { get; }
}
