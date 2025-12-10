using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Helpers
{
    public static class I18nHelper
    {
        public static string GetLocalizedString(string key) =>
        LocalizeDictionary.Instance.GetLocalizedObject(
                "neo-bpsys-wpf",
                "Locales.Lang",
                key,
                LocalizeDictionary.CurrentCulture)?
            .ToString() ?? string.Empty;
    }
}
