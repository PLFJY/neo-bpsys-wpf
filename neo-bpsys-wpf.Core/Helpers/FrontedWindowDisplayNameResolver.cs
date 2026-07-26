using System.Globalization;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 解析前台窗口注册的显示名称。
/// </summary>
/// <remarks>
/// Core 层只提供基于 <see cref="FrontedWindowRegistration.DisplayName"/> / <see cref="FrontedWindowRegistration.LocalId"/>
/// 的回退显示名。内置窗口的本地化显示名由 UI 层通过现有 resx（<c>Designer.Window.{LocalId}</c>）覆盖。
/// </remarks>
public static class FrontedWindowDisplayNameResolver
{
    /// <summary>
    /// 解析前台窗口注册面向用户的显示名称。
    /// </summary>
    /// <param name="registration">窗口注册。</param>
    /// <param name="language">请求的语言设置（保留用于 UI 层扩展，Core 层回退实现不使用）。</param>
    /// <param name="cultureInfo">当 <paramref name="language"/> 不是具体语言时使用的有效 UI 区域信息（保留用于 UI 层扩展）。</param>
    /// <returns>注册的回退显示名称。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="registration"/> 为 <see langword="null"/> 时抛出。</exception>
    public static string ResolveDisplayName(
        FrontedWindowRegistration registration,
        LanguageKey language,
        CultureInfo? cultureInfo = null)
    {
        ArgumentNullException.ThrowIfNull(registration);
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
}
