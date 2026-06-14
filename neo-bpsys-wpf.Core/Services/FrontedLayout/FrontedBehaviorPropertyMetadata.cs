using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Provides common behavior graph property metadata for editors and validators.
/// </summary>
public static class FrontedBehaviorPropertyMetadata
{
    private static readonly IReadOnlyDictionary<string, FrontedAnimatablePropertyMetadata> Metadata =
        CreateMetadata().ToDictionary(item => item.PropertyName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Common animatable property names exposed by the built-in behavior editor.
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
    ];

    /// <summary>
    /// Property names that apply to the generated control root.
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
    ];

    /// <summary>
    /// Property names that usually apply to the target control's main content.
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
    ];

    /// <summary>
    /// Property names that apply to runtime rectangle overlay layers.
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
    ];

    /// <summary>
    /// Gets property names recommended for a target layer.
    /// </summary>
    /// <param name="targetLayer">The target layer.</param>
    /// <param name="includeAll">Whether to include the reset-all sentinel value.</param>
    /// <returns>The recommended property names for the layer.</returns>
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
    /// Visibility values supported by WPF behavior animation.
    /// </summary>
    public static IReadOnlyList<string> VisibilityOptions { get; } = ["Visible", "Hidden", "Collapsed"];

    /// <summary>
    /// Gets metadata for a supported animatable property.
    /// </summary>
    /// <param name="propertyName">The stable property name.</param>
    /// <returns>The property metadata, or <c>null</c> when the property is unknown.</returns>
    public static FrontedAnimatablePropertyMetadata? Find(string? propertyName) =>
        string.IsNullOrWhiteSpace(propertyName) ? null : Metadata.GetValueOrDefault(propertyName);

    /// <summary>
    /// Determines whether a property name represents a numeric behavior value.
    /// </summary>
    /// <param name="propertyName">The property name to inspect.</param>
    /// <returns><c>true</c> when the property is numeric; otherwise <c>false</c>.</returns>
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
            "DurationMs");

    /// <summary>
    /// Determines whether a property name represents a color behavior value.
    /// </summary>
    /// <param name="propertyName">The property name to inspect.</param>
    /// <returns><c>true</c> when the property is color-like; otherwise <c>false</c>.</returns>
    public static bool IsColorProperty(string? propertyName) =>
        Is(propertyName, "FillColor", "StrokeColor", "TextColor", "TintColor", "Foreground", "Background");

    /// <summary>
    /// Determines whether a property name represents a visibility value.
    /// </summary>
    /// <param name="propertyName">The property name to inspect.</param>
    /// <returns><c>true</c> when the property is Visibility; otherwise <c>false</c>.</returns>
    public static bool IsVisibilityProperty(string? propertyName) => Is(propertyName, "Visibility");

    /// <summary>
    /// Validates a value according to the named behavior property.
    /// </summary>
    /// <param name="propertyName">The behavior property name.</param>
    /// <param name="value">The text value to validate.</param>
    /// <param name="message">The validation message when validation fails.</param>
    /// <returns><c>true</c> when the value is valid for the property; otherwise <c>false</c>.</returns>
    public static bool TryValidateValue(string? propertyName, string? value, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(propertyName) || string.Equals(propertyName, "All", StringComparison.OrdinalIgnoreCase))
        {
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

            if (Is(propertyName, "Width", "Height", "StrokeThickness", "FontSize", "DurationMs") && number < 0D)
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
    /// Determines whether the named property accepts percentage values such as <c>100%</c>.
    /// </summary>
    /// <param name="propertyName">The property name to inspect.</param>
    /// <returns><c>true</c> when percentage values are supported; otherwise <c>false</c>.</returns>
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
/// Describes editor guidance and constraints for an animatable property.
/// </summary>
public sealed class FrontedAnimatablePropertyMetadata
{
    /// <summary>
    /// Initializes a new property metadata instance.
    /// </summary>
    /// <param name="propertyName">The stable property name.</param>
    /// <param name="typeName">The stable value type name.</param>
    /// <param name="placeholder">The fallback input placeholder.</param>
    /// <param name="example">An example value.</param>
    /// <param name="descriptionKey">The localization key for contextual help.</param>
    /// <param name="min">The optional minimum numeric value.</param>
    /// <param name="max">The optional maximum numeric value.</param>
    /// <param name="allowedValues">The optional stable allowed values.</param>
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

    /// <summary>Gets the stable property name.</summary>
    public string PropertyName { get; }

    /// <summary>Gets the stable value type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the fallback editor placeholder.</summary>
    public string Placeholder { get; }

    /// <summary>Gets an example value.</summary>
    public string Example { get; }

    /// <summary>Gets the localization key for contextual help.</summary>
    public string DescriptionKey { get; }

    /// <summary>Gets the optional minimum numeric value.</summary>
    public double? Min { get; }

    /// <summary>Gets the optional maximum numeric value.</summary>
    public double? Max { get; }

    /// <summary>Gets the optional stable allowed values.</summary>
    public IReadOnlyList<string>? AllowedValues { get; }
}
