using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Provides common behavior graph property metadata for editors and validators.
/// </summary>
public static class FrontedBehaviorPropertyMetadata
{
    /// <summary>
    /// Common animatable property names exposed by the built-in behavior editor.
    /// </summary>
    public static IReadOnlyList<string> CommonPropertyNames { get; } =
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
        "StrokeThickness",
        "TextColor",
        "Foreground",
        "FontSize",
        "TintColor",
        "TintStrength",
        "TextureStrength"
    ];

    /// <summary>
    /// Visibility values supported by WPF behavior animation.
    /// </summary>
    public static IReadOnlyList<string> VisibilityOptions { get; } = ["Visible", "Hidden", "Collapsed"];

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
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
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
}
