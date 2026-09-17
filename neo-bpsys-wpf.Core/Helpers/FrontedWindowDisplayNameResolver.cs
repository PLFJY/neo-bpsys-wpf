using System.Globalization;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 解析前台窗口注册的显示名称。
/// </summary>
/// <remarks>
/// 用户自定义窗口优先使用注册中来自布局 JSON 的多语言字典；其他窗口使用
/// <see cref="FrontedWindowRegistration.DisplayName"/> / <see cref="FrontedWindowRegistration.LocalId"/> 回退。
/// 历史内置布局的 resx 兼容名称由 UI 层提供。
/// </remarks>
public static class FrontedWindowDisplayNameResolver
{
    /// <summary>
    /// 解析前台窗口注册面向用户的显示名称。
    /// </summary>
    /// <param name="registration">窗口注册。</param>
    /// <param name="language">请求的语言设置。</param>
    /// <param name="cultureInfo">当 <paramref name="language"/> 不是具体语言时使用的有效 UI 区域信息。</param>
    /// <returns>注册的回退显示名称。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="registration"/> 为 <see langword="null"/> 时抛出。</exception>
    public static string ResolveDisplayName(
        FrontedWindowRegistration registration,
        LanguageKey language,
        CultureInfo? cultureInfo = null)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (registration is FrontedCustomV3LayoutWindowRegistration customRegistration)
        {
            return ResolveDisplayName(
                customRegistration.DisplayNames,
                language,
                cultureInfo,
                GetFallbackDisplayName(registration));
        }

        return GetFallbackDisplayName(registration);
    }

    /// <summary>
    /// 根据语言设置和有效区域信息解析具体的受支持语言。
    /// </summary>
    /// <param name="language">请求的语言设置。</param>
    /// <param name="cultureInfo">有效的 UI 区域信息。</param>
    /// <returns>具体的语言键；当没有匹配的受支持语言时返回 <see langword="null"/>。</returns>
    public static LanguageKey? ResolveConcreteLanguage(LanguageKey language, CultureInfo? cultureInfo = null)
    {
        if (language is not LanguageKey.System and not LanguageKey.FollowApp)
        {
            return language;
        }

        var cultureName = (cultureInfo ?? CultureInfo.CurrentUICulture).Name;
        if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageKey.zh_Hans;
        }

        if (cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageKey.en_US;
        }

        if (cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageKey.ja_JP;
        }

        return null;
    }

    /// <summary>
    /// 获取前台窗口注册的非本地化回退显示名称。
    /// </summary>
    /// <param name="registration">窗口注册。</param>
    /// <returns>注册的显示名称；未配置显示名称时返回其局部窗口标识。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="registration"/> 为 <see langword="null"/> 时抛出。</exception>
    public static string GetFallbackDisplayName(FrontedWindowRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return string.IsNullOrWhiteSpace(registration.DisplayName)
            ? registration.LocalId
            : registration.DisplayName;
    }

    /// <summary>
    /// 按当前语言和固定回退顺序解析布局 JSON 中的显示名称。
    /// </summary>
    /// <param name="displayNames">布局 JSON 中的显示名称字典。</param>
    /// <param name="language">请求的语言设置。</param>
    /// <param name="cultureInfo">系统或跟随应用语言时使用的区域信息。</param>
    /// <param name="fallback">所有译名缺失时的回退文本。</param>
    /// <returns>解析后的显示名称。</returns>
    public static string ResolveDisplayName(
        IReadOnlyDictionary<string, string>? displayNames,
        LanguageKey language,
        CultureInfo? cultureInfo,
        string fallback)
    {
        var concreteLanguage = ResolveConcreteLanguage(language, cultureInfo);
        if (concreteLanguage is not null
            && displayNames is not null
            && displayNames.TryGetValue(GetLanguageCode(concreteLanguage.Value), out var current)
            && !string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        foreach (var code in new[] { "zh_Hans", "en_US", "ja_JP" })
        {
            if (displayNames is not null
                && displayNames.TryGetValue(code, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return fallback;
    }

    private static string GetLanguageCode(LanguageKey language) => language switch
    {
        LanguageKey.zh_Hans => "zh_Hans",
        LanguageKey.en_US => "en_US",
        LanguageKey.ja_JP => "ja_JP",
        _ => string.Empty
    };
}
