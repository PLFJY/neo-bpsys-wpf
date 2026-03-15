using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 提供应用级的本地化（i18n）辅助工具。
/// 使用 `WPFLocalizeExtension` 的 `LocalizeDictionary` 根据当前文化从资源中检索文本。
/// </summary>
/// <remarks>
/// 该类为静态工具类，无需实例化。调用 <see cref="GetLocalizedString(string)"/> 以根据资源键获取对应的本地化字符串。
/// 若在资源中找不到对应项，方法会返回原始的键值，便于降级显示或调试定位缺失的翻译项。
/// </remarks>
public static class I18nHelper
{
    /// <summary>
    /// 根据指定的资源键返回当前文化对应的本地化字符串。
    /// </summary>
    /// <param name="key">资源键（例如 "MainWindow.Title"）。不能为空。</param>
    /// <returns>若找到对应的本地化项，返回其字符串表示；否则返回传入的 <paramref name="key"/>。</returns>
    /// <example>
    /// var title = I18nHelper.GetLocalizedString("App.Title");
    /// </example>
    public static string GetLocalizedString(string key) =>
    LocalizeDictionary.Instance.GetLocalizedObject(
            "neo-bpsys-wpf",
            "Locales.Lang",
            key,
            LocalizeDictionary.CurrentCulture)?
        .ToString() ?? key;
}
