using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public static class FrontedTextLimitHelper
{
    public static string Clamp(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength < 0)
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public static bool IsTooLong(string? value, int maxLength)
    {
        return value is not null && value.Length > maxLength;
    }

    public static int GetMaxLengthForProperty(string propertyName, string? controlType = null)
    {
        if (string.Equals(propertyName, nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal))
        {
            return FrontedLayoutLimits.MaxBindingPathLength;
        }

        if (string.Equals(propertyName, nameof(TextFrontedControlConfig.Text), StringComparison.Ordinal))
        {
            return FrontedLayoutLimits.MaxStaticTextLength;
        }

        if (string.Equals(propertyName, nameof(TextFrontedControlConfig.FontFamily), StringComparison.Ordinal)
            || string.Equals(propertyName, "FontFamily", StringComparison.Ordinal))
        {
            return FrontedLayoutLimits.MaxFontFamilyLength;
        }

        if (string.Equals(propertyName, nameof(FrontedControlDesignItem.Name), StringComparison.Ordinal)
            || string.Equals(propertyName, "Name", StringComparison.Ordinal))
        {
            return FrontedLayoutLimits.MaxControlNameLength;
        }

        if (propertyName.Contains("Image", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("ResourcePath", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Path", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Source", StringComparison.OrdinalIgnoreCase))
        {
            return FrontedLayoutLimits.MaxResourcePathLength;
        }

        return int.MaxValue;
    }

    public static int GetMaxLengthForManifestField(string fieldName)
    {
        return fieldName switch
        {
            "PackageId" => FrontedLayoutLimits.MaxPackageIdLength,
            "Name" => FrontedLayoutLimits.MaxPackageNameLength,
            "Author" => FrontedLayoutLimits.MaxPackageAuthorLength,
            "MinVersion" => FrontedLayoutLimits.MaxPackageMinVersionLength,
            "Description" => FrontedLayoutLimits.MaxPackageDescriptionLength,
            _ => int.MaxValue
        };
    }
}
