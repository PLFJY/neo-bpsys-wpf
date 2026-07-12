using System.Globalization;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 从描述符本地化的国际化字典中解析前台窗口显示名称。
/// </summary>
public static class FrontedWindowDisplayNameResolver
{
    /// <summary>
    /// 解析前台窗口描述符面向用户的显示名称。
    /// </summary>
    /// <param name="descriptor">窗口描述符。</param>
    /// <param name="language">请求的语言设置。</param>
    /// <param name="cultureInfo">当 <paramref name="language"/> 不是具体语言时使用的有效 UI 区域信息。</param>
    /// <returns>本地化的显示名称，或描述符的回退显示名称。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="descriptor"/> 为 <see langword="null"/> 时抛出。</exception>
    public static string ResolveDisplayName(
        IFrontedWindowDescriptor descriptor,
        LanguageKey language,
        CultureInfo? cultureInfo = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var concreteLanguage = ResolveConcreteLanguage(language, cultureInfo);
        if (concreteLanguage.HasValue
            && descriptor.I18nDisplayNames is { Count: > 0 } names
            && names.TryGetValue(concreteLanguage.Value, out var localized)
            && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return GetFallbackDisplayName(descriptor);
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
    /// 获取前台窗口描述符的非本地化回退显示名称。
    /// </summary>
    /// <param name="descriptor">窗口描述符。</param>
    /// <returns>描述符的显示名称；未配置显示名称时返回其窗口类型名称。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="descriptor"/> 为 <see langword="null"/> 时抛出。</exception>
    public static string GetFallbackDisplayName(IFrontedWindowDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return string.IsNullOrWhiteSpace(descriptor.DisplayName)
            ? descriptor.WindowTypeName
            : descriptor.DisplayName;
    }
}
