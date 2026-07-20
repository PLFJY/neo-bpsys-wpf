using System.Globalization;
using WPFLocalizeExtension.Engine;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>使用主程序 I18nHelper 生成 Web Renderer 本地化快照。</summary>
public sealed class WebLocalizationProvider : IWebLocalizationProvider
{
    /// <inheritdoc />
    public WebLocalizationSnapshot Create(IReadOnlyCollection<string> requestedKeys)
    {
        var keys = new HashSet<string>(requestedKeys, StringComparer.Ordinal)
        {
            "GameProgressFree", "GameProgressGameOnlyFormat", "GameProgressGameOvertimeOnlyFormat",
            "GameProgressGameHalfFormat", "GameProgressGameOvertimeHalfFormat", "FirstHalf", "SecondHalf",
            "Sur", "Hun"
        };
        foreach (var map in Enum.GetNames<Map>()) keys.Add(map);
        var culture = LocalizeDictionary.CurrentCulture ?? CultureInfo.CurrentUICulture;
        static Dictionary<string, string> Read(string dictionary, IEnumerable<string> keys, CultureInfo culture) =>
            keys.Select(key => (key, value: I18nHelper.GetLocalizedString(dictionary, key, culture)))
                .Where(pair => !string.Equals(pair.value, pair.key, StringComparison.Ordinal))
                .ToDictionary(pair => pair.key, pair => pair.value, StringComparer.Ordinal);
        var anyHost = keys.Select(key => (key, value: I18nHelper.GetLocalizedStringFromAnyHostDictionary(key, culture)))
            .Where(pair => !string.Equals(pair.value, pair.key, StringComparison.Ordinal))
            .ToDictionary(pair => pair.key, pair => pair.value, StringComparer.Ordinal);
        return new WebLocalizationSnapshot(culture.Name, DateTime.UtcNow.Ticks,
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["Common"] = Read(AppI18nDictionaries.Common, keys, culture),
                ["Game"] = Read(AppI18nDictionaries.Game, keys, culture),
                ["Fronted"] = Read(AppI18nDictionaries.FrontManage, keys, culture)
            }, anyHost);
    }
}
