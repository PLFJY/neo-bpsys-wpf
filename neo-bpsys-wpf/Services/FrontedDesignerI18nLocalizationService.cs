using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 面向 WPF 宿主的、基于 I18n 的设计器 v3 本地化服务。
/// </summary>
public sealed class FrontedDesignerI18nLocalizationService : FrontedDesignerLocalizationService
{
    protected override string GetLocalizedOrFallback(string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        var localized = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? fallback : localized;
    }

    /// <summary>
    /// 返回指定属性选项的本地化显示名称。
    /// 对 <see cref="LanguageKey"/> 的属性，语言名选项直接映射到现有的 <c>zh_Hans</c>、<c>en_US</c>、<c>ja_JP</c> 资源键。
    /// </summary>
    public override string GetOptionDisplayName(string propertyName, object? value)
    {
        if (propertyName == "DisplayLanguage" && value is LanguageKey lang)
        {
            var key = lang switch
            {
                LanguageKey.zh_Hans => "zh_Hans",
                LanguageKey.en_US => "en_US",
                LanguageKey.ja_JP => "ja_JP",
                LanguageKey.System or LanguageKey.FollowApp => $"Designer.Option.{propertyName}.FollowApp",
                _ => $"Designer.Option.{propertyName}.{lang}"
            };
            var dictionary = lang is LanguageKey.System or LanguageKey.FollowApp
                ? AppI18nDictionaries.Designer
                : AppI18nDictionaries.Common;
            var localized = I18nHelper.GetLocalizedString(dictionary, key);
            return string.Equals(localized, key, StringComparison.Ordinal) ? key : localized;
        }

        return base.GetOptionDisplayName(propertyName, value);
    }
}
