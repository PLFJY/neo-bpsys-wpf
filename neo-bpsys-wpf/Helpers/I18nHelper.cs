using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 提供应用级的本地化（i18n）辅助工具。
/// 使用 `WPFLocalizeExtension` 的 `LocalizeDictionary` 根据当前文化从资源中检索文本。
/// </summary>
/// <remarks>
/// 该类为静态工具类，无需实例化。调用 <see cref="GetLocalizedString(string, string)"/> 以根据资源键获取对应的本地化字符串。
/// 若在资源中找不到对应项，方法会返回原始的键值，便于降级显示或调试定位缺失的翻译项。
/// <para>
/// 解析顺序：先尝试 <see cref="LocalizeDictionary"/>（WPF 运行时由 XAML 初始化 provider），
/// 若返回 null（例如在未配置 provider 的测试上下文中），则回退到直接从程序集嵌入资源创建的
/// <see cref="ResourceManager"/> 读取，保证本地化在任意上下文均可用。
/// </para>
/// </remarks>
public static class I18nHelper
{
    private static readonly ConcurrentDictionary<string, ResourceManager?> ResourceManagerCache = new();

    /// <summary>
    /// 根据指定的字典和资源键返回当前文化对应的本地化字符串。
    /// </summary>
    /// <param name="dictionary">目标字典名称（例如 <see cref="AppI18nDictionaries.Shell"/>）。不能为空。</param>
    /// <param name="key">资源键（例如 "MainWindow.Title"）。不能为空。</param>
    /// <returns>若找到对应的本地化项，返回其字符串表示；否则返回传入的 <paramref name="key"/>。</returns>
    /// <example>
    /// var title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "App.Title");
    /// </example>
    public static string GetLocalizedString(string dictionary, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionary);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return GetLocalizedStringCore(dictionary, key, LocalizeDictionary.CurrentCulture);
    }

    /// <summary>
    /// 根据指定的字典、资源键和指定文化返回对应的本地化字符串。
    /// 通过 <see cref="LocalizeDictionary"/> 解析，不再依赖生成的 <c>Lang</c> 类。
    /// </summary>
    /// <param name="dictionary">目标字典名称（例如 <see cref="AppI18nDictionaries.Shell"/>）。不能为空。</param>
    /// <param name="key">资源键（例如 "MainWindow.Title"）。不能为空。</param>
    /// <param name="culture">目标文化。</param>
    /// <returns>若找到对应的本地化项，返回其字符串表示；否则返回传入的 <paramref name="key"/>。</returns>
    public static string GetLocalizedString(string dictionary, string key, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionary);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);

        return GetLocalizedStringCore(dictionary, key, culture);
    }

    /// <summary>
    /// 在所有宿主资源族字典中按顺序查找指定资源键，返回首个命中的本地化字符串。
    /// 适用于无法预先确定归属字典的场景（例如前台布局控件按配置键解析任意域文本）。
    /// </summary>
    /// <param name="key">资源键。不能为空。</param>
    /// <returns>首个命中字典中的本地化字符串；若所有字典均未命中则返回 <paramref name="key"/>。</returns>
    public static string GetLocalizedStringFromAnyHostDictionary(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return GetLocalizedStringFromAnyHostDictionaryCore(key, LocalizeDictionary.CurrentCulture);
    }

    /// <summary>
    /// 在所有宿主资源族字典中按指定文化查找指定资源键，返回首个命中的本地化字符串。
    /// </summary>
    /// <param name="key">资源键。不能为空。</param>
    /// <param name="culture">目标文化。</param>
    /// <returns>首个命中字典中的本地化字符串；若所有字典均未命中则返回 <paramref name="key"/>。</returns>
    public static string GetLocalizedStringFromAnyHostDictionary(string key, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);
        return GetLocalizedStringFromAnyHostDictionaryCore(key, culture);
    }

    private static string GetLocalizedStringFromAnyHostDictionaryCore(string key, CultureInfo culture)
    {
        foreach (var dictionary in AppI18nDictionaries.AllDictionaries)
        {
            var result = GetLocalizedStringCore(dictionary, key, culture);
            if (result != key)
            {
                return result;
            }
        }

        return key;
    }

    private static string GetLocalizedStringCore(string dictionary, string key, CultureInfo culture)
    {
        var value = LocalizeDictionary.Instance.GetLocalizedObject(
            AppI18nDictionaries.Assembly,
            dictionary,
            key,
            culture);
        if (value is not null)
        {
            return value.ToString() ?? key;
        }

        var rm = GetResourceManager(dictionary);
        if (rm is not null)
        {
            var str = rm.GetString(key, culture);
            if (str is not null)
            {
                return str;
            }
        }

        return key;
    }

    /// <summary>
    /// 根据字典名查找宿主程序集嵌入资源对应的 <see cref="ResourceManager"/>，结果按字典名缓存。
    /// 嵌入资源清单名形如 <c>neo_bpsys_wpf.Locales.Score.resources</c>，而字典名为 <c>Locales.Score</c>，
    /// 此处通过后缀匹配定位实际清单名，避免硬编码根命名空间。
    /// </summary>
    /// <param name="dictionary">字典名（例如 <c>Locales.Score</c>）。</param>
    /// <returns>对应的 <see cref="ResourceManager"/>；若找不到匹配资源则返回 null。</returns>
    private static ResourceManager? GetResourceManager(string dictionary)
    {
        return ResourceManagerCache.GetOrAdd(dictionary, dict =>
        {
            try
            {
                var assembly = typeof(I18nHelper).Assembly;
                var suffix = "." + dict + ".resources";
                var match = Array.Find(
                    assembly.GetManifestResourceNames(),
                    n => n.EndsWith(suffix, StringComparison.Ordinal));
                if (match is null)
                {
                    return null;
                }

                var baseName = match.Substring(0, match.Length - ".resources".Length);
                return new ResourceManager(baseName, assembly);
            }
            catch
            {
                return null;
            }
        });
    }
}
