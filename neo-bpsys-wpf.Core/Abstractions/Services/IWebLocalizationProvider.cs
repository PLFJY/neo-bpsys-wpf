using System.Globalization;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>供主程序 Web Renderer 本地化桥接使用的布局文本请求。</summary>
public sealed record WebLocalizationRequest(string ControlId, string? LocalizationKey, string? FallbackText);

/// <summary>Web Renderer 的不可变本地化快照；所有值均为最终显示文本。</summary>
public sealed record WebLocalizationSnapshot(
    int SchemaVersion,
    long Revision,
    string Culture,
    IReadOnlyDictionary<string, string> StaticTexts,
    IReadOnlyDictionary<string, WebMapV2Localization> MapV2Texts);

/// <summary>一个 MapV2 地图及两个阵营的最终本地化文本。</summary>
public sealed record WebMapV2Localization(
    string MapKey,
    string MapDisplayName,
    string CampSurDisplayName,
    string CampHunDisplayName);

/// <summary>动态文本控件的最终显示文本。</summary>
public sealed record WebLocalizedControlState(string ControlId, string DisplayText);

/// <summary>Web Renderer 使用的后端本地化权威接口。</summary>
public interface IWebLocalizationProvider
{
    /// <summary>按指定 culture 和 revision 构建本地化快照。</summary>
    WebLocalizationSnapshot Create(IReadOnlyCollection<WebLocalizationRequest> requests, CultureInfo culture, long revision);

    /// <summary>解析 LocalizedText 的最终文本。</summary>
    WebLocalizedControlState ResolveLocalizedControl(string controlId, string? key, string? fallbackText, CultureInfo culture);

    /// <summary>解析地图名称的最终文本。</summary>
    WebLocalizedControlState ResolveMapName(string controlId, Map? map, string? emptyText, CultureInfo culture);

    /// <summary>解析阵营名称的最终文本。</summary>
    string ResolveCamp(Camp camp, CultureInfo culture);

    /// <summary>解析窗口注册的最终显示名称。</summary>
    string ResolveWindowDisplayName(FrontedWindowRegistration registration, LanguageKey language, CultureInfo culture);

    /// <summary>生成指定对局进度和控件配置的最终显示部件。</summary>
    WebGameProgressDisplayState CreateGameProgress(
        GameProgress progress,
        bool isBo3Mode,
        LanguageKey displayLanguage,
        GameProgressNumberStyle numberStyle,
        CultureInfo culture);
}

/// <summary>主程序 GameProgressDisplayHelper 的不可变 Web 投影。</summary>
public sealed record WebGameProgressDisplayState(
    bool IsValid,
    bool IsFree,
    int GameNumber,
    bool IsOvertime,
    string? Half,
    string FullText,
    string GameText,
    string HalfText,
    bool IsCjkCulture);

/// <summary>兼容旧注入点的对局进度服务接口。</summary>
public interface IWebGameProgressProvider
{
    /// <summary>生成指定进度、赛制和 culture 的最终显示数据。</summary>
    WebGameProgressDisplayState Create(
        GameProgress progress,
        bool isBo3Mode,
        CultureInfo culture,
        LanguageKey displayLanguage = LanguageKey.FollowApp,
        GameProgressNumberStyle numberStyle = GameProgressNumberStyle.Auto);
}
