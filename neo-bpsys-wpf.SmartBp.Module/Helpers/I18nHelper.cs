using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 模块内本地化帮助类，保持与宿主帮助类相同的调用形态。
/// </summary>
public static class I18nHelper
{
    /// <summary>
    /// 按资源键解析本地化文本。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <returns>解析到的本地化文本；未找到时返回资源键本身。</returns>
    public static string GetLocalizedString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        var value = LocalizeDictionary.Instance.GetLocalizedObject(key, null, LocalizeDictionary.CurrentCulture);
        return value?.ToString() ?? key;
    }
}
