using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Tutorial;

internal static class ProductTourFontResourceHelper
{
    private const string ProductTourFontFamilyKey = "ProductTourFontFamily";
    private const string PopFontFamilyKey = "POP1W5";
    private const string EnglishFontFamilyKey = "EssayText";

    public static void Apply(CultureInfo cultureInfo)
    {
        if (Application.Current == null)
        {
            return;
        }

        var sourceKey = string.Equals(cultureInfo.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase)
            ? EnglishFontFamilyKey
            : PopFontFamilyKey;
        Application.Current.Resources[ProductTourFontFamilyKey] =
            Application.Current.TryFindResource(sourceKey) as FontFamily
            ?? new FontFamily(sourceKey);
    }
}
