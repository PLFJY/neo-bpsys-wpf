using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 模块内本地化帮助类，解析模块自身程序集 (neo-bpsys-wpf.SmartBp.Module) 的资源。
/// </summary>
public static class I18nHelper
{
    private const string ModuleAssembly = "neo-bpsys-wpf.SmartBp.Module";
    private const string ModuleDictionary = "Locales.SmartBp";

    /// <summary>
    /// 按资源键解析模块自身程序集的本地化文本。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <returns>解析到的本地化文本；未找到时返回资源键本身。</returns>
    public static string GetLocalizedString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        var value = LocalizeDictionary.Instance.GetLocalizedObject(
            ModuleAssembly, ModuleDictionary, key, LocalizeDictionary.CurrentCulture);
        return value?.ToString() ?? key;
    }
}
