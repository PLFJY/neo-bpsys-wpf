using neo_bpsys_wpf.Core.Enums;
using System.Globalization;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 集中生成前台可见的地图名称文本。
/// </summary>
public static class MapNameDisplayHelper
{
    /// <summary>
    /// 格式化地图名称为前台可显示的本地化文本。
    /// </summary>
    /// <param name="map">要格式化的地图枚举值，可为 null。</param>
    /// <param name="emptyText">当地图为 null 时返回的默认文本。</param>
    /// <returns>本地化后的地图名称文本。</returns>
    public static string Format(Map? map, string? emptyText = null)
    {
        return Format(map, emptyText, null);
    }

    /// <summary>
    /// 格式化地图名称为前台可显示的本地化文本。
    /// </summary>
    /// <param name="map">要格式化的地图枚举值，可为 null。</param>
    /// <param name="emptyText">当地图为 null 时返回的默认文本。</param>
    /// <param name="culture">目标文化。为 null 时使用当前应用文化。</param>
    /// <returns>本地化后的地图名称文本。</returns>
    public static string Format(Map? map, string? emptyText, CultureInfo? culture)
    {
        if (map is null)
        {
            return emptyText ?? string.Empty;
        }

        return culture is null
            ? I18nHelper.GetLocalizedString(map.Value.ToString())
            : I18nHelper.GetLocalizedString(map.Value.ToString(), culture);
    }
}
