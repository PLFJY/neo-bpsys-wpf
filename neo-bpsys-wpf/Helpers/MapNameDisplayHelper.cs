using neo_bpsys_wpf.Core.Enums;

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
        if (map is null)
        {
            return emptyText ?? string.Empty;
        }

        return I18nHelper.GetLocalizedString(map.Value.ToString());
    }
}
