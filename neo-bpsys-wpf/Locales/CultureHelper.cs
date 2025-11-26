using neo_bpsys_wpf.Core.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Locales;

public static class CultureHelper
{
    private static readonly CultureInfo _systemCulture = CultureInfo.CurrentUICulture;

    public static string SetCultureFollowSystem()
    {
        LocalizeDictionary.Instance.Culture = _systemCulture;
        return _systemCulture.Name;
    }

    public static string SetCulture(string culture)
    {
        LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo(culture);
        return culture;
    }

    public static string SetCulture(LanguageKey key) => key == LanguageKey.System ?
        SetCultureFollowSystem()
        : SetCulture(key.ToString().Replace('_', '-'));
}
