using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Helpers;
using System.Globalization;

namespace neo_bpsys_wpf.Services;

/// <summary>将主程序 GameProgressDisplayHelper 结果投影给 Web Renderer。</summary>
public sealed class WebGameProgressProvider(IWebLocalizationProvider localizationProvider) : IWebGameProgressProvider
{
    /// <inheritdoc />
    public WebGameProgressDisplayState Create(
        GameProgress progress,
        bool isBo3Mode,
        CultureInfo culture,
        LanguageKey displayLanguage = LanguageKey.FollowApp,
        GameProgressNumberStyle numberStyle = GameProgressNumberStyle.Auto) =>
        localizationProvider.CreateGameProgress(progress, isBo3Mode, displayLanguage, numberStyle, culture);
}
