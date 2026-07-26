using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// Web Renderer 的主程序本地化权威桥接。
/// </summary>
/// <remarks>
/// 该服务只接收显式 <see cref="CultureInfo"/>，所有返回值都是最终显示文本。
/// 浏览器永远不会收到主程序资源字典或资源 key 查找规则。
/// </remarks>
public sealed class WebRendererLocalizationBridge(ILogger<WebRendererLocalizationBridge>? logger = null)
    : IWebLocalizationProvider
{
    private const int SchemaVersion = 1;
    private static readonly ConcurrentDictionary<string, byte> ReportedMissing = new(StringComparer.Ordinal);
    private readonly ILogger<WebRendererLocalizationBridge>? _logger = logger;

    /// <inheritdoc />
    public WebLocalizationSnapshot Create(
        IReadOnlyCollection<WebLocalizationRequest> requests,
        CultureInfo culture,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(culture);

        var staticTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.ControlId))
            {
                continue;
            }

            staticTexts[request.ControlId] = ResolveLocalizedControl(
                request.ControlId,
                request.LocalizationKey,
                request.FallbackText,
                culture).DisplayText;
        }

        var maps = new Dictionary<string, WebMapV2Localization>(StringComparer.Ordinal);
        foreach (var map in Enum.GetValues<Map>())
        {
            var key = map.ToString();
            var displayName = MapNameDisplayHelper.Format(map, null, culture);
            ReportMissingIfEqual(AppI18nDictionaries.Game, key, displayName, culture);
            maps[key] = new(
                key,
                displayName,
                ResolveCamp(Camp.Sur, culture),
                ResolveCamp(Camp.Hun, culture));
        }

        return new WebLocalizationSnapshot(
            SchemaVersion,
            revision,
            culture.Name,
            new Dictionary<string, string>(staticTexts, StringComparer.Ordinal),
            new Dictionary<string, WebMapV2Localization>(maps, StringComparer.Ordinal));
    }

    /// <inheritdoc />
    public WebLocalizedControlState ResolveLocalizedControl(
        string controlId,
        string? key,
        string? fallbackText,
        CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlId);
        ArgumentNullException.ThrowIfNull(culture);

        if (string.IsNullOrWhiteSpace(key))
        {
            return new(controlId, fallbackText ?? string.Empty);
        }

        var value = I18nHelper.GetLocalizedStringFromAnyHostDictionary(key, culture);
        if (string.Equals(value, key, StringComparison.Ordinal))
        {
            ReportMissing("AnyHost", key, culture);
            value = fallbackText ?? key;
        }

        return new(controlId, value);
    }

    /// <inheritdoc />
    public WebLocalizedControlState ResolveMapName(
        string controlId,
        Map? map,
        string? emptyText,
        CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlId);
        ArgumentNullException.ThrowIfNull(culture);

        var value = MapNameDisplayHelper.Format(map, emptyText, culture);
        if (map is not null)
        {
            ReportMissingIfEqual(AppI18nDictionaries.Game, map.Value.ToString(), value, culture);
        }

        return new(controlId, value);
    }

    /// <inheritdoc />
    public string ResolveCamp(Camp camp, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var key = camp.ToString();
        var value = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, key, culture);
        ReportMissingIfEqual(AppI18nDictionaries.Common, key, value, culture);
        return value;
    }

    /// <inheritdoc />
    public string ResolveWindowDisplayName(
        FrontedWindowRegistration registration,
        LanguageKey language,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(culture);
        return FrontedWindowDisplayNameResolver.ResolveDisplayName(registration, language, culture);
    }

    /// <inheritdoc />
    public WebGameProgressDisplayState CreateGameProgress(
        GameProgress progress,
        bool isBo3Mode,
        LanguageKey displayLanguage,
        GameProgressNumberStyle numberStyle,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var effectiveCulture = GameProgressDisplayHelper.ResolveCulture(displayLanguage, culture);
        var valid = (int)progress is >= -1 and <= 11;
        if (!valid)
        {
            return new(false, false, 0, false, null, string.Empty, string.Empty, string.Empty,
                GameProgressDisplayHelper.IsCjkCulture(effectiveCulture));
        }
        var parts = GameProgressDisplayHelper.GetParts(progress, isBo3Mode, effectiveCulture, numberStyle);
        return new(
            valid,
            parts.IsFree,
            parts.GameNumber ?? 0,
            parts.IsOvertime,
            parts.Half?.ToString(),
            parts.FullText,
            parts.GameText,
            parts.HalfText,
            GameProgressDisplayHelper.IsCjkCulture(effectiveCulture));
    }

    private void ReportMissingIfEqual(string dictionary, string key, string value, CultureInfo culture)
    {
        if (string.Equals(key, value, StringComparison.Ordinal))
        {
            ReportMissing(dictionary, key, culture);
        }
    }

    private void ReportMissing(string dictionary, string key, CultureInfo culture)
    {
        var diagnostic = $"LocalizationMissing:{dictionary}:{key}:{culture.Name}";
        if (!ReportedMissing.TryAdd(diagnostic, 0))
        {
            return;
        }

        _logger?.LogWarning("{Diagnostic}", diagnostic);
    }
}
